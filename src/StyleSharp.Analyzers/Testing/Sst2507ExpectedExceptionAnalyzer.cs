// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a test method that declares its expected failure with an expected-exception attribute
/// (SST2507) — MSTest's <c>[ExpectedException(typeof(T))]</c> or NUnit's legacy <c>[ExpectedException]</c> —
/// instead of asserting the specific operation with <c>Assert.Throws&lt;T&gt;</c>. The attribute passes
/// whenever <em>any</em> statement in the whole method throws the named type, so it cannot tell the
/// operation under test apart from a setup line that throws the same exception.
/// </summary>
/// <remarks>
/// <para>
/// The whole rule is gated at compilation start on an expected-exception attribute type and at least one
/// test-framework marker resolving. A compilation that references neither — most modern NUnit code, which
/// dropped the attribute in NUnit 3 — registers nothing and pays nothing.
/// </para>
/// <para>
/// The clean path is a syntactic prepass: a method must syntactically carry an attribute named
/// <c>ExpectedException</c> (with or without the <c>Attribute</c> suffix) before anything binds. Only then
/// is the method symbol resolved and its attributes inspected, to confirm the attribute really is the
/// framework's expected-exception attribute and that the method really is a test. No code fix is offered,
/// because turning the attribute into an assertion means choosing which statement was expected to throw.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2507ExpectedExceptionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The bare simple name of the expected-exception attribute.</summary>
    private const string ExpectedExceptionName = "ExpectedException";

    /// <summary>The expected-exception attribute simple name written with its <c>Attribute</c> suffix.</summary>
    private const string ExpectedExceptionAttributeName = "ExpectedExceptionAttribute";

    /// <summary>The metadata names of the expected-exception attributes the rule recognises.</summary>
    private static readonly string[] ExpectedExceptionMetadataNames =
    [
        "Microsoft.VisualStudio.TestTools.UnitTesting.ExpectedExceptionAttribute",
        "NUnit.Framework.ExpectedExceptionAttribute",
    ];

    /// <summary>The metadata names of the test-method markers across the supported frameworks.</summary>
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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(TestingRules.ExpectedException);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the rule only when an expected-exception attribute and a test marker both resolve.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var expectedExceptionMarkers = Resolve(context.Compilation, ExpectedExceptionMetadataNames);
        if (expectedExceptionMarkers.Length == 0)
        {
            return;
        }

        var testMarkers = Resolve(context.Compilation, TestMarkerMetadataNames);
        if (testMarkers.Length == 0)
        {
            return;
        }

        var facts = new TestingFacts(expectedExceptionMarkers, testMarkers);
        context.RegisterSyntaxNodeAction(nodeContext => AnalyzeMethod(nodeContext, facts), SyntaxKind.MethodDeclaration);
    }

    /// <summary>Reports a test method whose expected failure is declared with an expected-exception attribute.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="facts">The resolved expected-exception and test-marker attribute types.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, TestingFacts facts)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (FindExpectedExceptionAttribute(method) is not { } expectedExceptionAttribute)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is not { } symbol
            || !IsExpectedExceptionOnTest(symbol, facts))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(TestingRules.ExpectedException, expectedExceptionAttribute.GetLocation()));
    }

    /// <summary>Resolves the metadata names that are present in the compilation.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <param name="metadataNames">The metadata names to resolve.</param>
    /// <returns>The resolved types; empty when none are present.</returns>
    private static ImmutableArray<INamedTypeSymbol> Resolve(Compilation compilation, string[] metadataNames)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(metadataNames.Length);
        for (var i = 0; i < metadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(metadataNames[i]) is { } type)
            {
                builder.Add(type);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Returns the first attribute a method syntactically writes as <c>ExpectedException</c>.</summary>
    /// <param name="method">The method declaration to scan.</param>
    /// <returns>The matching attribute, or <see langword="null"/> when the method has none.</returns>
    private static AttributeSyntax? FindExpectedExceptionAttribute(MethodDeclarationSyntax method)
    {
        foreach (var attributeList in method.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (IsExpectedExceptionName(attribute.Name))
                {
                    return attribute;
                }
            }
        }

        return null;
    }

    /// <summary>Returns whether an attribute name's simple form is <c>ExpectedException</c>.</summary>
    /// <param name="name">The attribute name syntax.</param>
    /// <returns><see langword="true"/> when the trailing simple name is the expected-exception attribute.</returns>
    private static bool IsExpectedExceptionName(NameSyntax name)
    {
        while (name is QualifiedNameSyntax qualified)
        {
            name = qualified.Right;
        }

        return name is IdentifierNameSyntax identifier
            && (identifier.Identifier.ValueText == ExpectedExceptionName
                || identifier.Identifier.ValueText == ExpectedExceptionAttributeName);
    }

    /// <summary>Returns whether a method binds both an expected-exception attribute and a test marker.</summary>
    /// <param name="method">The method symbol.</param>
    /// <param name="facts">The resolved expected-exception and test-marker attribute types.</param>
    /// <returns><see langword="true"/> when the method is a test carrying an expected-exception attribute.</returns>
    private static bool IsExpectedExceptionOnTest(IMethodSymbol method, TestingFacts facts)
    {
        var attributes = method.GetAttributes();
        var hasExpectedException = false;
        var hasTestMarker = false;
        for (var i = 0; i < attributes.Length; i++)
        {
            var attributeClass = attributes[i].AttributeClass;
            if (!hasExpectedException && MatchesAny(attributeClass, facts.ExpectedExceptionMarkers))
            {
                hasExpectedException = true;
            }

            if (!hasTestMarker && MatchesAny(attributeClass, facts.TestMarkers))
            {
                hasTestMarker = true;
            }

            if (hasExpectedException && hasTestMarker)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is, or derives from, one of the marker types.</summary>
    /// <param name="type">The attribute type to test.</param>
    /// <param name="markers">The resolved marker types.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> matches or derives from a marker.</returns>
    private static bool MatchesAny(INamedTypeSymbol? type, ImmutableArray<INamedTypeSymbol> markers)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            for (var j = 0; j < markers.Length; j++)
            {
                if (SymbolEqualityComparer.Default.Equals(current, markers[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The resolved expected-exception and test-marker attribute types for one compilation.</summary>
    /// <param name="ExpectedExceptionMarkers">The expected-exception attribute types present in the compilation.</param>
    /// <param name="TestMarkers">The test-method marker attribute types present in the compilation.</param>
    private readonly record struct TestingFacts(
        ImmutableArray<INamedTypeSymbol> ExpectedExceptionMarkers,
        ImmutableArray<INamedTypeSymbol> TestMarkers);
}
