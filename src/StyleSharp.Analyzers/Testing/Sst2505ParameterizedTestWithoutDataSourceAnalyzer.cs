// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a test method that declares parameters but has no data source (SST2505), so the runner has
/// nothing to fill those parameters with and the case is skipped, errored, or silently never run — its
/// assertions never execute while the test appears green.
/// </summary>
/// <remarks>
/// <para>
/// The recognized data source depends on the framework, and the rule resolves each one it can:
/// xUnit needs an attribute deriving from <c>Xunit.Sdk.DataAttribute</c> (which covers
/// <c>[InlineData]</c>, <c>[MemberData]</c>, <c>[ClassData]</c>, and custom data attributes); NUnit
/// needs an attribute implementing <c>NUnit.Framework.Interfaces.ITestBuilder</c> (<c>[TestCase]</c>,
/// <c>[TestCaseSource]</c>) on the method or <c>NUnit.Framework.Interfaces.IParameterDataSource</c>
/// (<c>[Values]</c>, <c>[Range]</c>, <c>[Random]</c>) on a parameter; MSTest needs an attribute
/// implementing <c>Microsoft.VisualStudio.TestTools.UnitTesting.ITestDataSource</c> (<c>[DataRow]</c>,
/// <c>[DynamicData]</c>); TUnit needs an attribute implementing <c>TUnit.Core.IDataSourceAttribute</c>.
/// </para>
/// <para>
/// The whole rule is gated at compilation start on at least one test-attribute marker resolving, so a
/// project that references no test framework pays nothing. The clean path is a syntactic prepass: the
/// method must declare a parameter and carry an attribute written with a known test-attribute name
/// before anything binds. Only then are the method's and parameters' attributes bound to confirm a real
/// test attribute is present and that no recognized data source is — the conservative condition under
/// which the case is reported. A parameterless test and a parameterized test that already has any data
/// source are never reported.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2505ParameterizedTestWithoutDataSourceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the xUnit base attribute every data attribute derives from.</summary>
    private const string XunitDataAttributeMetadataName = "Xunit.Sdk.DataAttribute";

    /// <summary>The metadata names of the attributes that mark a method as a test.</summary>
    private static readonly string[] TestMarkerMetadataNames =
    [
        "Xunit.FactAttribute",
        "Xunit.TheoryAttribute",
        "NUnit.Framework.TestAttribute",
        "NUnit.Framework.TestCaseAttribute",
        "NUnit.Framework.TestCaseSourceAttribute",
        "NUnit.Framework.TheoryAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute",
        "TUnit.Core.TestAttribute",
    ];

    /// <summary>The metadata names of the interfaces a framework's data-source attribute implements.</summary>
    private static readonly string[] DataSourceInterfaceMetadataNames =
    [
        "Microsoft.VisualStudio.TestTools.UnitTesting.ITestDataSource",
        "NUnit.Framework.Interfaces.ITestBuilder",
        "NUnit.Framework.Interfaces.IParameterDataSource",
        "TUnit.Core.IDataSourceAttribute",
    ];

    /// <summary>The simple names, with and without the suffix, that a test-marking attribute is written as.</summary>
    private static readonly HashSet<string> TestAttributeSimpleNames = new(StringComparer.Ordinal)
    {
        "Fact", "FactAttribute",
        "Theory", "TheoryAttribute",
        "Test", "TestAttribute",
        "TestCase", "TestCaseAttribute",
        "TestCaseSource", "TestCaseSourceAttribute",
        "TestMethod", "TestMethodAttribute",
        "DataTestMethod", "DataTestMethodAttribute",
    };

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(TestingRules.ParameterizedTestWithoutDataSource);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var symbols = FrameworkSymbols.Resolve(start.Compilation);
            if (symbols is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMethod(nodeContext, symbols), SyntaxKind.MethodDeclaration);
        });
    }

    /// <summary>Returns whether an attribute's simple name is one a test framework uses to mark a test.</summary>
    /// <param name="name">The attribute's written simple name.</param>
    /// <returns><see langword="true"/> when the name is a known test-attribute name, with or without the suffix.</returns>
    internal static bool IsTestAttributeSimpleName(string name) => TestAttributeSimpleNames.Contains(name);

    /// <summary>Analyzes one method declaration for a parameterized test with no data source.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="symbols">The resolved test-framework symbols.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, FrameworkSymbols symbols)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.ParameterList.Parameters.Count == 0
            || !HasTestAttributeName(method.AttributeLists))
        {
            return;
        }

        if (!IsReportableTest(context.SemanticModel, method, symbols, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            TestingRules.ParameterizedTestWithoutDataSource,
            method.Identifier.GetLocation(),
            method.Identifier.ValueText));
    }

    /// <summary>Returns whether a parameterized, test-attribute-named method binds to a real test with no data source.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="method">The method declaration.</param>
    /// <param name="symbols">The resolved test-framework symbols.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the method should be reported.</returns>
    private static bool IsReportableTest(SemanticModel model, MethodDeclarationSyntax method, FrameworkSymbols symbols, CancellationToken cancellationToken)
    {
        var hasTestMarker = false;
        var lists = method.AttributeLists;
        for (var i = 0; i < lists.Count; i++)
        {
            var attributes = lists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (model.GetSymbolInfo(attributes[j], cancellationToken).Symbol is not IMethodSymbol { ContainingType: { } attributeClass })
                {
                    continue;
                }

                if (symbols.IsDataSource(attributeClass))
                {
                    return false;
                }

                hasTestMarker = hasTestMarker || symbols.IsTestMarker(attributeClass);
            }
        }

        return hasTestMarker
            && !ParameterCarriesDataSource(model, method.ParameterList, symbols, cancellationToken)
            && HasDataRequiringParameter(model, method, symbols, cancellationToken);
    }

    /// <summary>Returns whether the method declares a parameter that a data source would have to fill.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="method">The method declaration.</param>
    /// <param name="symbols">The resolved test-framework symbols.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>
    /// <see langword="true"/> when at least one parameter is not framework-injected. A test whose only
    /// parameters are framework-injected — a <c>CancellationToken</c> the runner supplies for a timeout —
    /// needs no data source, so it is not reported.
    /// </returns>
    private static bool HasDataRequiringParameter(SemanticModel model, MethodDeclarationSyntax method, FrameworkSymbols symbols, CancellationToken cancellationToken)
    {
        if (model.GetDeclaredSymbol(method, cancellationToken) is not { } methodSymbol)
        {
            return true;
        }

        var parameters = methodSymbol.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (!symbols.IsInjectedParameterType(parameters[i].Type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether any attribute on the method is written with a known test-attribute name.</summary>
    /// <param name="attributeLists">The method's attribute lists.</param>
    /// <returns><see langword="true"/> when a test-attribute name is present.</returns>
    private static bool HasTestAttributeName(SyntaxList<AttributeListSyntax> attributeLists)
    {
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (IsTestAttributeSimpleName(GetSimpleName(attributes[j].Name)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether any of the method's parameters carries a recognized data-source attribute.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="parameterList">The method's parameter list.</param>
    /// <param name="symbols">The resolved test-framework symbols.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a per-parameter data source is present.</returns>
    private static bool ParameterCarriesDataSource(SemanticModel model, ParameterListSyntax parameterList, FrameworkSymbols symbols, CancellationToken cancellationToken)
    {
        var parameters = parameterList.Parameters;
        for (var p = 0; p < parameters.Count; p++)
        {
            var lists = parameters[p].AttributeLists;
            for (var i = 0; i < lists.Count; i++)
            {
                var attributes = lists[i].Attributes;
                for (var j = 0; j < attributes.Count; j++)
                {
                    if (model.GetSymbolInfo(attributes[j], cancellationToken).Symbol is IMethodSymbol { ContainingType: { } attributeClass }
                        && symbols.IsDataSource(attributeClass))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Gets the rightmost identifier of a possibly qualified or aliased attribute name.</summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The simple name, or an empty string.</returns>
    private static string GetSimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };

    /// <summary>The test-framework symbols resolved once per compilation the rule needs to classify attributes.</summary>
    private sealed class FrameworkSymbols
    {
        /// <summary>The resolved test-attribute markers a method's attribute must be to count as a test.</summary>
        private readonly INamedTypeSymbol[] _testMarkers;

        /// <summary>The resolved xUnit data-attribute base, or <see langword="null"/> when xUnit is not referenced.</summary>
        private readonly INamedTypeSymbol? _xunitDataAttribute;

        /// <summary>The resolved data-source interfaces a data attribute implements across frameworks.</summary>
        private readonly INamedTypeSymbol[] _dataSourceInterfaces;

        /// <summary>The resolved <c>System.Threading.CancellationToken</c> type a runner injects, never a data source.</summary>
        private readonly INamedTypeSymbol? _cancellationToken;

        /// <summary>Initializes a new instance of the <see cref="FrameworkSymbols"/> class.</summary>
        /// <param name="testMarkers">The resolved test-attribute markers.</param>
        /// <param name="xunitDataAttribute">The resolved xUnit data-attribute base, or <see langword="null"/>.</param>
        /// <param name="dataSourceInterfaces">The resolved data-source interfaces.</param>
        /// <param name="cancellationToken">The resolved <c>CancellationToken</c> type, or <see langword="null"/>.</param>
        private FrameworkSymbols(INamedTypeSymbol[] testMarkers, INamedTypeSymbol? xunitDataAttribute, INamedTypeSymbol[] dataSourceInterfaces, INamedTypeSymbol? cancellationToken)
        {
            _testMarkers = testMarkers;
            _xunitDataAttribute = xunitDataAttribute;
            _dataSourceInterfaces = dataSourceInterfaces;
            _cancellationToken = cancellationToken;
        }

        /// <summary>Resolves the test-framework symbols, or <see langword="null"/> when no test framework is referenced.</summary>
        /// <param name="compilation">The analyzed compilation.</param>
        /// <returns>The resolved symbols, or <see langword="null"/> when no test-attribute marker resolves.</returns>
        public static FrameworkSymbols? Resolve(Compilation compilation)
        {
            var markers = ResolveAll(compilation, TestMarkerMetadataNames);
            if (markers.Length == 0)
            {
                return null;
            }

            return new FrameworkSymbols(
                markers,
                compilation.GetTypeByMetadataName(XunitDataAttributeMetadataName),
                ResolveAll(compilation, DataSourceInterfaceMetadataNames),
                compilation.GetTypeByMetadataName("System.Threading.CancellationToken"));
        }

        /// <summary>Returns whether a parameter's type is one a test runner injects rather than one a data source fills.</summary>
        /// <param name="type">The parameter's type.</param>
        /// <returns><see langword="true"/> for <c>System.Threading.CancellationToken</c>.</returns>
        public bool IsInjectedParameterType(ITypeSymbol type)
            => _cancellationToken is not null && SymbolEqualityComparer.Default.Equals(type, _cancellationToken);

        /// <summary>Returns whether an attribute type is or derives from a resolved test-attribute marker.</summary>
        /// <param name="attributeClass">The attribute's type.</param>
        /// <returns><see langword="true"/> when the type marks a method as a test.</returns>
        public bool IsTestMarker(INamedTypeSymbol attributeClass)
        {
            for (var type = attributeClass; type is not null; type = type.BaseType)
            {
                var definition = type.OriginalDefinition;
                for (var m = 0; m < _testMarkers.Length; m++)
                {
                    if (SymbolEqualityComparer.Default.Equals(definition, _testMarkers[m]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Returns whether an attribute type is a recognized data source for any framework.</summary>
        /// <param name="attributeClass">The attribute's type.</param>
        /// <returns><see langword="true"/> when the type supplies test data.</returns>
        public bool IsDataSource(INamedTypeSymbol attributeClass)
        {
            if (_xunitDataAttribute is not null)
            {
                for (var type = attributeClass; type is not null; type = type.BaseType)
                {
                    if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _xunitDataAttribute))
                    {
                        return true;
                    }
                }
            }

            if (_dataSourceInterfaces.Length == 0)
            {
                return false;
            }

            var interfaces = attributeClass.AllInterfaces;
            for (var i = 0; i < interfaces.Length; i++)
            {
                var definition = interfaces[i].OriginalDefinition;
                for (var d = 0; d < _dataSourceInterfaces.Length; d++)
                {
                    if (SymbolEqualityComparer.Default.Equals(definition, _dataSourceInterfaces[d]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Resolves each metadata name that binds, packed into an array with no gaps.</summary>
        /// <param name="compilation">The analyzed compilation.</param>
        /// <param name="metadataNames">The metadata names to resolve.</param>
        /// <returns>The resolved types; empty when none bind.</returns>
        private static INamedTypeSymbol[] ResolveAll(Compilation compilation, string[] metadataNames)
        {
            var resolved = new INamedTypeSymbol[metadataNames.Length];
            var count = 0;
            for (var i = 0; i < metadataNames.Length; i++)
            {
                if (compilation.GetTypeByMetadataName(metadataNames[i]) is { } type)
                {
                    resolved[count] = type;
                    count++;
                }
            }

            if (count == metadataNames.Length)
            {
                return resolved;
            }

            var trimmed = new INamedTypeSymbol[count];
            for (var i = 0; i < count; i++)
            {
                trimmed[i] = resolved[i];
            }

            return trimmed;
        }
    }
}
