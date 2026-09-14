// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a concrete class marked as a test fixture that declares no test method of its own and inherits
/// none (SST2504) — an inert fixture the runner loads but never exercises.
/// </summary>
/// <remarks>
/// <para>
/// The rule is scoped to the two frameworks whose test classes are marked explicitly: MSTest, where a class
/// carries the test-class attribute, and NUnit, where it carries the test-fixture attribute. xUnit has no
/// such attribute — a class is a test class by containing a fact or theory — so xUnit is out of scope, and
/// the whole rule is gated on at least one of the MSTest/NUnit class-attribute markers resolving in the
/// analyzed compilation.
/// </para>
/// <para>
/// Several shapes are deliberately not reported. An <b>abstract</b> class is a legitimate shared base fixture.
/// A class that declares its own test method — under any recognized framework's method attribute — is
/// exercised. And a class that inherits from a base type carrying a test-class attribute or a test method is
/// exercised through the inherited tests; the base chain is walked so those are left alone.
/// </para>
/// <para>
/// The clean path is a syntactic prepass: a class that does not carry a test-class attribute by name, or that
/// is abstract, is dismissed before anything binds. Only a class that syntactically looks like a test fixture
/// is bound, its attribute confirmed against a resolved marker, and its members and base chain examined.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2504EmptyTestClassAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata names of the class-attribute markers that put a class in scope.</summary>
    private static readonly string[] ClassMarkerMetadataNames =
    [
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute",
        "NUnit.Framework.TestFixtureAttribute",
    ];

    /// <summary>The metadata names of the method-attribute markers that make a method a test.</summary>
    private static readonly string[] TestMethodMarkerMetadataNames =
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

    /// <summary>The unqualified test-class attribute names probed in the syntactic prepass.</summary>
    private static readonly string[] ClassMarkerSimpleNames =
    [
        "TestClass",
        "TestClassAttribute",
        "TestFixture",
        "TestFixtureAttribute",
    ];

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(TestingRules.EmptyTestClass);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var compilation = start.Compilation;
            var classMarkers = new Lazy<INamedTypeSymbol[]>(() => MetadataTypeLookup.ResolveAll(compilation, ClassMarkerMetadataNames));
            var methodMarkers = new Lazy<INamedTypeSymbol[]>(() => MetadataTypeLookup.ResolveAll(compilation, TestMethodMarkerMetadataNames));
            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, classMarkers, methodMarkers), SyntaxKind.ClassDeclaration);
        });
    }

    /// <summary>Analyzes one class declaration for a test fixture with no tests.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="classMarkers">The lazily resolved test-class attribute markers.</param>
    /// <param name="methodMarkers">The lazily resolved test-method attribute markers.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, Lazy<INamedTypeSymbol[]> classMarkers, Lazy<INamedTypeSymbol[]> methodMarkers)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;
        if (ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.AbstractKeyword) || !CarriesTestClassAttributeName(declaration.AttributeLists))
        {
            return;
        }

        var resolvedClassMarkers = classMarkers.Value;
        if (resolvedClassMarkers.Length == 0
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } classSymbol
            || !SymbolFacts.HasAttributeDerivedFromAny(classSymbol.GetAttributes(), resolvedClassMarkers)
            || HasTestMethod(classSymbol, methodMarkers.Value)
            || InheritsTests(classSymbol, resolvedClassMarkers, methodMarkers.Value))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            TestingRules.EmptyTestClass,
            declaration.Identifier.GetLocation(),
            declaration.Identifier.ValueText));
    }

    /// <summary>Returns whether any attribute is written with a test-class attribute's simple name.</summary>
    /// <param name="attributeLists">The class's attribute lists.</param>
    /// <returns><see langword="true"/> when a name matches, before any binding.</returns>
    /// <remarks>
    /// The written name's last token is its unqualified identifier for every shape a test-class attribute
    /// takes — <c>TestClass</c>, <c>Framework.TestClass</c>, or <c>global::Framework.TestClass</c> — so it is
    /// the whole syntactic filter, and nothing binds until it matches.
    /// </remarks>
    private static bool CarriesTestClassAttributeName(SyntaxList<AttributeListSyntax> attributeLists)
    {
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (StringArrays.ContainsOrdinal(ClassMarkerSimpleNames, attributes[j].Name.GetLastToken().ValueText))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether the class declares a test method of its own.</summary>
    /// <param name="type">The class symbol, whose declared members span every partial part.</param>
    /// <param name="methodMarkers">The resolved test-method markers.</param>
    /// <returns><see langword="true"/> when a declared method carries a test-method attribute.</returns>
    private static bool HasTestMethod(INamedTypeSymbol type, INamedTypeSymbol[] methodMarkers)
    {
        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol method && SymbolFacts.HasAttributeDerivedFromAny(method.GetAttributes(), methodMarkers))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the class inherits tests from a base type in its chain.</summary>
    /// <param name="classSymbol">The class under analysis.</param>
    /// <param name="classMarkers">The resolved test-class markers.</param>
    /// <param name="methodMarkers">The resolved test-method markers.</param>
    /// <returns><see langword="true"/> when a base type is a test fixture or declares a test method.</returns>
    /// <remarks>
    /// A base that carries a test-class attribute, or that declares a test method, means the derived class is
    /// exercised through inherited tests (or is a specialization of a shared fixture); either way it is not an
    /// inert leftover, so it is left alone.
    /// </remarks>
    private static bool InheritsTests(INamedTypeSymbol classSymbol, INamedTypeSymbol[] classMarkers, INamedTypeSymbol[] methodMarkers)
    {
        for (var baseType = classSymbol.BaseType; baseType is not null && baseType.SpecialType != SpecialType.System_Object; baseType = baseType.BaseType)
        {
            if (SymbolFacts.HasAttributeDerivedFromAny(baseType.GetAttributes(), classMarkers) || HasTestMethod(baseType, methodMarkers))
            {
                return true;
            }
        }

        return false;
    }
}
