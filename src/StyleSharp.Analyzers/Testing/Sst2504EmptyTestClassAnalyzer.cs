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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(TestingRules.EmptyTestClass);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var classMarkers = ResolveMarkers(start.Compilation, ClassMarkerMetadataNames);
            if (classMarkers.Length == 0)
            {
                return;
            }

            var methodMarkers = ResolveMarkers(start.Compilation, TestMethodMarkerMetadataNames);
            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, classMarkers, methodMarkers), SyntaxKind.ClassDeclaration);
        });
    }

    /// <summary>Analyzes one class declaration for a test fixture with no tests.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="classMarkers">The resolved test-class attribute markers.</param>
    /// <param name="methodMarkers">The resolved test-method attribute markers.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol[] classMarkers, INamedTypeSymbol[] methodMarkers)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;
        if (IsAbstract(declaration.Modifiers) || !CarriesTestClassAttributeName(declaration.AttributeLists))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } classSymbol
            || !HasAttributeFrom(classSymbol.GetAttributes(), classMarkers)
            || HasTestMethod(classSymbol, methodMarkers)
            || InheritsTests(classSymbol, classMarkers, methodMarkers))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            TestingRules.EmptyTestClass,
            declaration.Identifier.GetLocation(),
            declaration.Identifier.ValueText));
    }

    /// <summary>Resolves the non-null named-type symbols for a set of metadata names, right-sized.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <param name="metadataNames">The metadata names to resolve.</param>
    /// <returns>The resolved markers, an array no longer than <paramref name="metadataNames"/>.</returns>
    private static INamedTypeSymbol[] ResolveMarkers(Compilation compilation, string[] metadataNames)
    {
        var buffer = new INamedTypeSymbol[metadataNames.Length];
        var count = 0;
        for (var i = 0; i < metadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(metadataNames[i]) is { } marker)
            {
                buffer[count++] = marker;
            }
        }

        if (count == buffer.Length)
        {
            return buffer;
        }

        var result = new INamedTypeSymbol[count];
        Array.Copy(buffer, result, count);
        return result;
    }

    /// <summary>Returns whether a modifier list contains <c>abstract</c>.</summary>
    /// <param name="modifiers">The declaration's modifiers.</param>
    /// <returns><see langword="true"/> when the class is abstract.</returns>
    private static bool IsAbstract(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.AbstractKeyword))
            {
                return true;
            }
        }

        return false;
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
                if (IsClassMarkerName(attributes[j].Name.GetLastToken().ValueText))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether an unqualified attribute name is one of the test-class markers.</summary>
    /// <param name="name">The unqualified attribute name.</param>
    /// <returns><see langword="true"/> when the name matches a marker.</returns>
    private static bool IsClassMarkerName(string name)
    {
        for (var i = 0; i < ClassMarkerSimpleNames.Length; i++)
        {
            if (string.Equals(name, ClassMarkerSimpleNames[i], StringComparison.Ordinal))
            {
                return true;
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
            if (members[i] is IMethodSymbol method && HasAttributeFrom(method.GetAttributes(), methodMarkers))
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
            if (HasAttributeFrom(baseType.GetAttributes(), classMarkers) || HasTestMethod(baseType, methodMarkers))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether any attribute binds to a resolved marker (equal to or derived from it).</summary>
    /// <param name="attributes">The attributes to inspect.</param>
    /// <param name="markers">The resolved markers to match against.</param>
    /// <returns><see langword="true"/> when an attribute matches a marker.</returns>
    private static bool HasAttributeFrom(ImmutableArray<AttributeData> attributes, INamedTypeSymbol[] markers)
    {
        for (var i = 0; i < attributes.Length; i++)
        {
            if (MatchesAnyMarker(attributes[i].AttributeClass, markers))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an attribute class equals or derives from any resolved marker.</summary>
    /// <param name="attributeClass">The bound attribute class, or <see langword="null"/>.</param>
    /// <param name="markers">The resolved markers.</param>
    /// <returns><see langword="true"/> when a marker is in the attribute class's base chain.</returns>
    private static bool MatchesAnyMarker(INamedTypeSymbol? attributeClass, INamedTypeSymbol[] markers)
    {
        for (var current = attributeClass; current is not null; current = current.BaseType)
        {
            for (var i = 0; i < markers.Length; i++)
            {
                if (SymbolEqualityComparer.Default.Equals(current, markers[i]))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
