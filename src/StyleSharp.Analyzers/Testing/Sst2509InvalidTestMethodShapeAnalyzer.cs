// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a method that carries a test attribute but whose signature the runner cannot execute (SST2509): it is
/// non-public, a generic method with no parameters, or returns a type other than <c>void</c>, <c>Task</c>,
/// <c>ValueTask</c>, <c>Task&lt;T&gt;</c>, or <c>ValueTask&lt;T&gt;</c>. Such a method is discovered as a test and then
/// silently skipped, so it appears to pass while never running.
/// </summary>
/// <remarks>
/// <para>
/// The rule reuses the plain test-method attribute markers — xUnit <c>[Fact]</c>/<c>[Theory]</c>, NUnit
/// <c>[Test]</c>/<c>[TestCase]</c>/<c>[TestCaseSource]</c>/<c>[Theory]</c>, MSTest <c>[TestMethod]</c>/<c>[DataTestMethod]</c>,
/// and TUnit <c>[Test]</c>. The public requirement is applied only for xUnit, NUnit, and MSTest, which do not discover
/// non-public methods; TUnit is excluded from the public check because it is not a universal requirement there. The
/// generic and return-type requirements are universal and apply to every framework. A static method is never reported —
/// some frameworks run static test methods.
/// </para>
/// <para>
/// The whole rule is gated at compilation start on at least one test-attribute marker resolving, so a project that
/// references no test framework pays nothing. The clean path is a syntactic prepass: a method must carry an attribute
/// written with a known test-attribute name and must not already have a runnable shape (public, non-generic, returning
/// <c>void</c>) before anything binds. Only a method that looks like a test with a suspect shape is bound — to confirm a
/// real test attribute is present and to classify the exact violation from the method symbol.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2509InvalidTestMethodShapeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the TUnit test marker, which does not universally require a public method.</summary>
    private const string TUnitTestMarkerMetadataName = "TUnit.Core.TestAttribute";

    /// <summary>The metadata names of the attributes that mark a method as a test the framework discovers only when public.</summary>
    private static readonly string[] PublicRequiredMarkerMetadataNames =
    [
        "Xunit.FactAttribute",
        "Xunit.TheoryAttribute",
        "NUnit.Framework.TestAttribute",
        "NUnit.Framework.TestCaseAttribute",
        "NUnit.Framework.TestCaseSourceAttribute",
        "NUnit.Framework.TheoryAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute",
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
        => ImmutableArrays.Of(TestingRules.InvalidTestMethodShape);

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

    /// <summary>Analyzes one method declaration for a test-method shape the runner cannot execute.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="symbols">The resolved test-framework symbols.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, FrameworkSymbols symbols)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!HasTestAttributeName(method.AttributeLists) || IsSyntacticallyRunnableShape(method))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is not { } methodSymbol)
        {
            return;
        }

        var (isTest, requiresPublic) = ClassifyTestAttributes(context, method.AttributeLists, symbols);
        if (!isTest)
        {
            return;
        }

        var reason = DescribeViolation(methodSymbol, requiresPublic, symbols);
        if (reason is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            TestingRules.InvalidTestMethodShape,
            method.Identifier.GetLocation(),
            method.Identifier.ValueText,
            reason));
    }

    /// <summary>Binds the method's attributes to determine whether it is a real test and whether its framework requires a public method.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="attributeLists">The method's attribute lists.</param>
    /// <param name="symbols">The resolved test-framework symbols.</param>
    /// <returns>Whether a real test attribute is present and whether the matched framework discovers only public methods.</returns>
    private static (bool IsTest, bool RequiresPublic) ClassifyTestAttributes(
        SyntaxNodeAnalysisContext context,
        SyntaxList<AttributeListSyntax> attributeLists,
        FrameworkSymbols symbols)
    {
        var isTest = false;
        var requiresPublic = false;
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (context.SemanticModel.GetSymbolInfo(attributes[j], context.CancellationToken).Symbol is not IMethodSymbol { ContainingType: { } attributeClass })
                {
                    continue;
                }

                if (symbols.IsPublicRequiredMarker(attributeClass))
                {
                    isTest = true;
                    requiresPublic = true;
                }
                else if (symbols.IsTUnitMarker(attributeClass))
                {
                    isTest = true;
                }
            }
        }

        return (isTest, requiresPublic);
    }

    /// <summary>Describes the first shape violation on a confirmed test method, or <see langword="null"/> when it is runnable.</summary>
    /// <param name="method">The test method symbol.</param>
    /// <param name="requiresPublic">Whether the matched framework discovers only public test methods.</param>
    /// <param name="symbols">The resolved test-framework symbols.</param>
    /// <returns>A clause describing the violation, or <see langword="null"/> when the shape is runnable.</returns>
    private static string? DescribeViolation(IMethodSymbol method, bool requiresPublic, FrameworkSymbols symbols)
    {
        if (requiresPublic && method.DeclaredAccessibility != Accessibility.Public)
        {
            return "is not public";
        }

        if (method.IsGenericMethod && method.Parameters.Length == 0)
        {
            return "is a generic method with no parameters, so its type argument cannot be inferred";
        }

        var returnType = method.ReturnType;
        return !method.ReturnsVoid && returnType.TypeKind != TypeKind.Error && !symbols.IsRunnableReturnType(returnType)
            ? $"returns '{returnType.ToDisplayString()}', which is not void, Task, or ValueTask"
            : null;
    }

    /// <summary>Returns whether the method's shape is syntactically already runnable, so binding can be skipped.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns>
    /// <see langword="true"/> when the method is written with a <c>public</c> modifier, no type parameters, and a
    /// <c>void</c> return — the shape every framework runs, which needs no further checking.
    /// </returns>
    private static bool IsSyntacticallyRunnableShape(MethodDeclarationSyntax method)
        => method.TypeParameterList is null
            && method.Modifiers.Any(SyntaxKind.PublicKeyword)
            && method.ReturnType is PredefinedTypeSyntax predefined
            && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);

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
                if (TestAttributeSimpleNames.Contains(GetSimpleName(attributes[j].Name)))
                {
                    return true;
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

    /// <summary>The test-framework symbols resolved once per compilation the rule needs to classify attributes and returns.</summary>
    private sealed class FrameworkSymbols
    {
        /// <summary>The resolved markers whose framework discovers only public test methods; unresolved slots stay <see langword="null"/>.</summary>
        private readonly INamedTypeSymbol?[] _publicRequiredMarkers;

        /// <summary>The resolved TUnit test marker, or <see langword="null"/> when TUnit is not referenced.</summary>
        private readonly INamedTypeSymbol? _tunitMarker;

        /// <summary>The resolved <c>System.Threading.Tasks.Task</c>, or <see langword="null"/> when it is absent.</summary>
        private readonly INamedTypeSymbol? _task;

        /// <summary>The resolved <c>System.Threading.Tasks.Task&lt;T&gt;</c> definition, or <see langword="null"/> when it is absent.</summary>
        private readonly INamedTypeSymbol? _taskOfT;

        /// <summary>The resolved <c>System.Threading.Tasks.ValueTask</c>, or <see langword="null"/> when it is absent.</summary>
        private readonly INamedTypeSymbol? _valueTask;

        /// <summary>The resolved <c>System.Threading.Tasks.ValueTask&lt;T&gt;</c> definition, or <see langword="null"/> when it is absent.</summary>
        private readonly INamedTypeSymbol? _valueTaskOfT;

        /// <summary>Initializes a new instance of the <see cref="FrameworkSymbols"/> class.</summary>
        /// <param name="publicRequiredMarkers">The resolved public-required test markers.</param>
        /// <param name="tunitMarker">The resolved TUnit test marker, or <see langword="null"/>.</param>
        /// <param name="task">The resolved <c>Task</c> type, or <see langword="null"/>.</param>
        /// <param name="taskOfT">The resolved <c>Task&lt;T&gt;</c> definition, or <see langword="null"/>.</param>
        /// <param name="valueTask">The resolved <c>ValueTask</c> type, or <see langword="null"/>.</param>
        /// <param name="valueTaskOfT">The resolved <c>ValueTask&lt;T&gt;</c> definition, or <see langword="null"/>.</param>
        private FrameworkSymbols(
            INamedTypeSymbol?[] publicRequiredMarkers,
            INamedTypeSymbol? tunitMarker,
            INamedTypeSymbol? task,
            INamedTypeSymbol? taskOfT,
            INamedTypeSymbol? valueTask,
            INamedTypeSymbol? valueTaskOfT)
        {
            _publicRequiredMarkers = publicRequiredMarkers;
            _tunitMarker = tunitMarker;
            _task = task;
            _taskOfT = taskOfT;
            _valueTask = valueTask;
            _valueTaskOfT = valueTaskOfT;
        }

        /// <summary>Resolves the test-framework symbols, or <see langword="null"/> when no test framework is referenced.</summary>
        /// <param name="compilation">The analyzed compilation.</param>
        /// <returns>The resolved symbols, or <see langword="null"/> when no test-attribute marker resolves.</returns>
        public static FrameworkSymbols? Resolve(Compilation compilation)
        {
            var publicRequiredMarkers = new INamedTypeSymbol?[PublicRequiredMarkerMetadataNames.Length];
            var anyPublicRequired = false;
            for (var i = 0; i < PublicRequiredMarkerMetadataNames.Length; i++)
            {
                var marker = compilation.GetTypeByMetadataName(PublicRequiredMarkerMetadataNames[i]);
                publicRequiredMarkers[i] = marker;
                anyPublicRequired = anyPublicRequired || marker is not null;
            }

            var tunitMarker = compilation.GetTypeByMetadataName(TUnitTestMarkerMetadataName);
            if (!anyPublicRequired && tunitMarker is null)
            {
                return null;
            }

            return new FrameworkSymbols(
                publicRequiredMarkers,
                tunitMarker,
                compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
                compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),
                compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask"),
                compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1"));
        }

        /// <summary>Returns whether an attribute type is or derives from a marker whose framework requires a public test method.</summary>
        /// <param name="attributeClass">The attribute's type.</param>
        /// <returns><see langword="true"/> when the type marks a method as an xUnit, NUnit, or MSTest test.</returns>
        public bool IsPublicRequiredMarker(INamedTypeSymbol attributeClass)
        {
            for (var type = attributeClass; type is not null; type = type.BaseType)
            {
                var definition = type.OriginalDefinition;
                for (var m = 0; m < _publicRequiredMarkers.Length; m++)
                {
                    if (SymbolEqualityComparer.Default.Equals(definition, _publicRequiredMarkers[m]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Returns whether an attribute type is or derives from the TUnit test marker.</summary>
        /// <param name="attributeClass">The attribute's type.</param>
        /// <returns><see langword="true"/> when the type marks a method as a TUnit test.</returns>
        public bool IsTUnitMarker(INamedTypeSymbol attributeClass)
        {
            for (var type = attributeClass; type is not null; type = type.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _tunitMarker))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns whether a return type is one the runner awaits or runs to completion.</summary>
        /// <param name="type">The method's return type.</param>
        /// <returns><see langword="true"/> for <c>Task</c>, <c>ValueTask</c>, <c>Task&lt;T&gt;</c>, or <c>ValueTask&lt;T&gt;</c>.</returns>
        public bool IsRunnableReturnType(ITypeSymbol type)
        {
            if (SymbolEqualityComparer.Default.Equals(type, _task) || SymbolEqualityComparer.Default.Equals(type, _valueTask))
            {
                return true;
            }

            var definition = type.OriginalDefinition;
            return SymbolEqualityComparer.Default.Equals(definition, _taskOfT)
                || SymbolEqualityComparer.Default.Equals(definition, _valueTaskOfT);
        }
    }
}
