// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a test method that declares parameters but has no data source (SST2505), so the runner has
/// nothing to fill those parameters with and the case is skipped, errored, or silently never run — its
/// assertions never execute while the test appears green.
/// </summary>
/// <remarks>
/// <para>
/// The recognized data source depends on the framework, and the rule resolves each one it can:
/// xUnit v2 needs an attribute deriving from <c>Xunit.Sdk.DataAttribute</c> and xUnit v3 one implementing
/// <c>Xunit.v3.IDataAttribute</c> (which covers
/// <c>[InlineData]</c>, <c>[MemberData]</c>, <c>[ClassData]</c>, and custom data attributes); NUnit
/// needs an attribute implementing <c>NUnit.Framework.Interfaces.ITestBuilder</c> (<c>[TestCase]</c>,
/// <c>[TestCaseSource]</c>) on the method or <c>NUnit.Framework.Interfaces.IParameterDataSource</c>
/// (<c>[Values]</c>, <c>[Range]</c>, <c>[Random]</c>) on a parameter; MSTest needs an attribute
/// implementing <c>Microsoft.VisualStudio.TestTools.UnitTesting.ITestDataSource</c> (<c>[DataRow]</c>,
/// <c>[DynamicData]</c>); TUnit needs an attribute implementing <c>TUnit.Core.IDataSourceAttribute</c>.
/// </para>
/// <para>
/// Framework symbols are resolved only for syntactically eligible methods. The clean path is a syntactic prepass:
/// the method must declare a parameter and carry an attribute written with a known test-attribute name
/// before anything binds. Only then are the method's and parameters' attributes bound to confirm a real
/// test attribute is present and that no recognized data source is — the conservative condition under
/// which the case is reported. A parameterless test and a parameterized test that already has any data
/// source are never reported.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2505ParameterizedTestWithoutDataSourceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The slot of the <c>System.Threading.CancellationToken</c> type a runner injects, never a data source.</summary>
    private const int CancellationTokenSlot = 0;

    /// <summary>The slot of the xUnit v2 base attribute every data attribute derives from.</summary>
    private const int XunitDataAttributeSlot = 1;

    /// <summary>The first slot of the interfaces a framework's data-source attribute implements.</summary>
    private const int FirstDataSourceInterfaceSlot = 2;

    /// <summary>The first test-marker slot.</summary>
    private const int FirstMarkerSlot = 7;

    /// <summary>The injected token, the xUnit v2 data base, the data-source interfaces, then the test markers, one slot each.</summary>
    private static readonly string[] FrameworkMetadataNames = TestAttributeNames.CreateMarkerMetadataNames(
        "System.Threading.CancellationToken",
        "Xunit.Sdk.DataAttribute",
        "Xunit.v3.IDataAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.ITestDataSource",
        "NUnit.Framework.Interfaces.ITestBuilder",
        "NUnit.Framework.Interfaces.IParameterDataSource",
        "TUnit.Core.IDataSourceAttribute");

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(TestingRules.ParameterizedTestWithoutDataSource);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataTypeSlots(compilation, FrameworkMetadataNames),
            AnalyzeMethod,
            SyntaxKind.MethodDeclaration);
    }

    /// <summary>Analyzes one method declaration for a parameterized test with no data source.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="symbols">The framework types resolved one slot at a time per compilation.</param>
    private static void AnalyzeMethod(in SyntaxNodeAnalysisContext context, LazyMetadataTypeSlots symbols)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.ParameterList.Parameters.Count == 0
            || !SyntaxNames.AnyAttributeNamed(method.AttributeLists, TestAttributeNames.IsTestAttributeName))
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
    private static bool IsReportableTest(SemanticModel model, MethodDeclarationSyntax method, LazyMetadataTypeSlots symbols, CancellationToken cancellationToken)
    {
        if (model.GetDeclaredSymbol(method, cancellationToken) is not { } methodSymbol)
        {
            return false;
        }

        var hasTestMarker = false;
        return !ContainsDataSource(methodSymbol.GetAttributes(), method, symbols, cancellationToken, ref hasTestMarker)
            && !ContainsDataSource(methodSymbol.GetReturnTypeAttributes(), method, symbols, cancellationToken, ref hasTestMarker)
            && hasTestMarker
            && !ParameterCarriesDataSource(model, method.ParameterList, symbols, cancellationToken)
            && HasDataRequiringParameter(model, method, symbols, cancellationToken);
    }

    /// <summary>Checks a declaration's attributes for data sources while collecting test markers.</summary>
    /// <param name="attributes">The declaration symbol's attributes.</param>
    /// <param name="method">The method whose written attributes are being inspected.</param>
    /// <param name="symbols">The resolved test-framework symbols.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="hasTestMarker">Whether a test marker has been found.</param>
    /// <returns>True when the written declaration contains a recognized data source.</returns>
    private static bool ContainsDataSource(
        ImmutableArray<AttributeData> attributes,
        MethodDeclarationSyntax method,
        LazyMetadataTypeSlots symbols,
        CancellationToken cancellationToken,
        ref bool hasTestMarker)
    {
        for (var i = 0; i < attributes.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attribute = attributes[i];
            if (attribute.ApplicationSyntaxReference is not { } reference
                || reference.SyntaxTree != method.SyntaxTree
                || !method.Span.Contains(reference.Span)
                || attribute.AttributeConstructor is not { ContainingType: { } attributeClass })
            {
                continue;
            }

            if (IsDataSource(symbols, attributeClass))
            {
                return true;
            }

            hasTestMarker = hasTestMarker || IsTestMarker(symbols, attributeClass);
        }

        return false;
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
    private static bool HasDataRequiringParameter(SemanticModel model, MethodDeclarationSyntax method, LazyMetadataTypeSlots symbols, CancellationToken cancellationToken)
    {
        if (model.GetDeclaredSymbol(method, cancellationToken) is not { } methodSymbol)
        {
            return true;
        }

        var parameters = methodSymbol.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (!IsInjectedParameterType(symbols, parameters[i].Type))
            {
                return true;
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
    private static bool ParameterCarriesDataSource(SemanticModel model, ParameterListSyntax parameterList, LazyMetadataTypeSlots symbols, CancellationToken cancellationToken)
    {
        if (model.GetDeclaredSymbol(parameterList.Parent!, cancellationToken) is not IMethodSymbol methodSymbol)
        {
            return false;
        }

        var parameters = methodSymbol.Parameters;
        for (var p = 0; p < parameters.Length; p++)
        {
            if (parameterList.Parameters[p].AttributeLists.Count == 0)
            {
                continue;
            }

            var attributes = parameters[p].GetAttributes();
            for (var i = 0; i < attributes.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attribute = attributes[i];
                if (attribute.ApplicationSyntaxReference is { } reference
                    && reference.SyntaxTree == parameterList.SyntaxTree
                    && parameterList.Span.Contains(reference.Span)
                    && attribute.AttributeConstructor is { ContainingType: { } attributeClass }
                    && IsDataSource(symbols, attributeClass))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a parameter's type is one a test runner injects rather than one a data source fills.</summary>
    /// <param name="symbols">The framework types resolved one slot at a time per compilation.</param>
    /// <param name="type">The parameter's type.</param>
    /// <returns><see langword="true"/> for <c>System.Threading.CancellationToken</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInjectedParameterType(LazyMetadataTypeSlots symbols, ITypeSymbol type) =>
        type is INamedTypeSymbol named && symbols.IsAny(named, CancellationTokenSlot, CancellationTokenSlot + 1);

    /// <summary>Returns whether an attribute type is or derives from a test-attribute marker.</summary>
    /// <param name="symbols">The framework types resolved one slot at a time per compilation.</param>
    /// <param name="attributeClass">The attribute's type.</param>
    /// <returns><see langword="true"/> when the type marks a method as a test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTestMarker(LazyMetadataTypeSlots symbols, INamedTypeSymbol attributeClass) =>
        symbols.IsOrDerivesFromAny(attributeClass, FirstMarkerSlot, FrameworkMetadataNames.Length);

    /// <summary>Returns whether an attribute type is a recognized data source for any framework.</summary>
    /// <param name="symbols">The framework types resolved one slot at a time per compilation.</param>
    /// <param name="attributeClass">The attribute's type.</param>
    /// <returns><see langword="true"/> when the type supplies test data.</returns>
    private static bool IsDataSource(LazyMetadataTypeSlots symbols, INamedTypeSymbol attributeClass)
    {
        if (symbols.IsOrDerivesFromAny(attributeClass, XunitDataAttributeSlot, FirstDataSourceInterfaceSlot))
        {
            return true;
        }

        var interfaces = attributeClass.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (symbols.IsAny(interfaces[i].OriginalDefinition, FirstDataSourceInterfaceSlot, FirstMarkerSlot))
            {
                return true;
            }
        }

        return false;
    }
}
