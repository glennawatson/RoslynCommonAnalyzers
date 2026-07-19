// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a test method whose body contains no assertion and no expected-exception check (SST2500). Such a
/// test runs, passes, and verifies nothing, so a regression in the code it appears to cover slips through green.
/// A test method is one carrying a test attribute from a supported framework — xUnit (<c>Fact</c>, <c>Theory</c>),
/// NUnit (<c>Test</c>, <c>TestCase</c>, <c>TestCaseSource</c>, <c>Theory</c>), MSTest (<c>TestMethod</c>,
/// <c>DataTestMethod</c>), or TUnit (<c>Test</c>).
/// </summary>
/// <remarks>
/// <para>
/// The whole rule is gated at compilation start on at least one framework's test attribute resolving; a project
/// that references none registers nothing and pays nothing. The clean path is a syntactic prepass: a method is
/// ignored outright unless one of its attributes is spelled with a known test-attribute simple name (<c>Fact</c>,
/// <c>Theory</c>, <c>Test</c>, <c>TestCase</c>, <c>TestCaseSource</c>, <c>TestMethod</c>, <c>DataTestMethod</c>,
/// with or without the <c>Attribute</c> suffix). Only a method that clears that name check is bound, and only
/// then to confirm one of its attributes really is a resolved framework marker.
/// </para>
/// <para>
/// The report decision is deliberately conservative to keep false positives near zero, because proving a body
/// verifies nothing would otherwise need interprocedural analysis (walking into every method it calls), which is
/// not done here. The rule reports only when it can prove the body verifies nothing from the body alone: every
/// invocation and object creation in the body must resolve to a method whose containing assembly is a platform
/// (BCL) assembly — <c>mscorlib</c>, <c>netstandard</c>, <c>System.Private.CoreLib</c>, or an assembly whose name
/// is <c>System</c> or starts with <c>System.</c> — and is not one of the in-BCL verification helpers
/// (<c>System.Diagnostics.Debug</c>, <c>System.Diagnostics.Trace</c>, or <c>System.Diagnostics.Contracts.Contract</c>).
/// A body with no invocations or object creations at all (an empty test, or one that only computes locals)
/// qualifies too.
/// </para>
/// <para>
/// Anything the rule cannot prove is a harmless platform call leaves the method silent: a call into the user's
/// own source (which might be an assertion helper), a call into any non-platform referenced assembly (an
/// assertion framework such as the framework's own <c>Assert</c>/<c>Assume</c>/<c>CollectionAssert</c>/
/// <c>StringAssert</c>/<c>Assert.That</c>, <c>Assert.Throws</c>/<c>ThrowsAsync</c>/<c>Record.Exception</c>, or a
/// fluent third-party shape such as <c>.Should()</c>/<c>.ShouldBe(...)</c>), an object creation of a non-platform
/// type, an unresolved call, or a <c>throw</c> (a deliberately pending test does not pass). A method carrying an
/// expected-exception attribute (MSTest's <c>ExpectedException</c> or a subclass of its base) is also silent,
/// because it verifies by asserting that an exception is thrown.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2500TestWithoutAssertionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of MSTest's expected-exception attribute base type.</summary>
    private const string ExpectedExceptionBaseMetadataName =
        "Microsoft.VisualStudio.TestTools.UnitTesting.ExpectedExceptionBaseAttribute";

    /// <summary>The metadata name of MSTest's concrete expected-exception attribute.</summary>
    private const string ExpectedExceptionMetadataName =
        "Microsoft.VisualStudio.TestTools.UnitTesting.ExpectedExceptionAttribute";

    /// <summary>The suffix every attribute class carries but that is optional at the use site.</summary>
    private const string AttributeSuffix = "Attribute";

    /// <summary>The metadata names of the supported frameworks' test-method marker attributes.</summary>
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
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(TestingRules.TestAssertsNothing);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Resolves the framework markers once, then registers the per-method check only when at least one resolves.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var markers = ResolveMarkers(context.Compilation);
        if (markers.Length == 0)
        {
            return;
        }

        var expectedException = context.Compilation.GetTypeByMetadataName(ExpectedExceptionBaseMetadataName)
            ?? context.Compilation.GetTypeByMetadataName(ExpectedExceptionMetadataName);
        var facts = new TestFrameworkFacts(markers, expectedException);
        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, facts), SyntaxKind.MethodDeclaration);
    }

    /// <summary>Reports one test method whose body provably verifies nothing.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="facts">The resolved framework markers and expected-exception base type.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, TestFrameworkFacts facts)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!HasTestAttributeName(method.AttributeLists))
        {
            return;
        }

        var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body is null)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is not IMethodSymbol symbol)
        {
            return;
        }

        if (!CarriesTestMarker(symbol, facts, out var hasExpectedException) || hasExpectedException)
        {
            return;
        }

        if (BodyMightVerify(body, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            TestingRules.TestAssertsNothing,
            method.Identifier.GetLocation(),
            method.Identifier.ValueText));
    }

    /// <summary>Resolves the referenced frameworks' test markers into an exact-size array of the non-null ones.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <returns>The resolved marker types; empty when no supported framework is referenced.</returns>
    private static INamedTypeSymbol[] ResolveMarkers(Compilation compilation)
    {
        var resolved = new INamedTypeSymbol?[TestMarkerMetadataNames.Length];
        var count = 0;
        for (var i = 0; i < TestMarkerMetadataNames.Length; i++)
        {
            var marker = compilation.GetTypeByMetadataName(TestMarkerMetadataNames[i]);
            if (marker is not null)
            {
                resolved[count++] = marker;
            }
        }

        if (count == 0)
        {
            return [];
        }

        var markers = new INamedTypeSymbol[count];
        for (var i = 0; i < count; i++)
        {
            markers[i] = resolved[i]!;
        }

        return markers;
    }

    /// <summary>Returns whether any attribute in the lists is spelled with a known test-attribute simple name.</summary>
    /// <param name="attributeLists">The method's attribute lists.</param>
    /// <returns><see langword="true"/> when at least one attribute could be a test marker by name alone.</returns>
    private static bool HasTestAttributeName(SyntaxList<AttributeListSyntax> attributeLists)
    {
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (GetSimpleName(attributes[j].Name) is { } name && IsTestAttributeName(name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Extracts the trailing simple identifier of an attribute name, ignoring any qualification.</summary>
    /// <param name="name">The attribute's name node.</param>
    /// <returns>The simple identifier text, or <see langword="null"/> when it cannot be read.</returns>
    private static string? GetSimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => GetSimpleName(qualified.Right),
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Returns whether an attribute simple name matches a supported framework's test attribute.</summary>
    /// <param name="name">The attribute's simple identifier, with or without the <c>Attribute</c> suffix.</param>
    /// <returns><see langword="true"/> for a known test-attribute name.</returns>
    private static bool IsTestAttributeName(string name)
    {
        var bare = name.EndsWith(AttributeSuffix, StringComparison.Ordinal)
            ? name.Substring(0, name.Length - AttributeSuffix.Length)
            : name;

        return bare is "Fact" or "Theory" or "Test" or "TestCase" or "TestCaseSource" or "TestMethod" or "DataTestMethod";
    }

    /// <summary>Confirms a method carries a resolved test marker, and reports whether it also declares an expected exception.</summary>
    /// <param name="symbol">The bound method symbol.</param>
    /// <param name="facts">The resolved framework markers and expected-exception base type.</param>
    /// <param name="hasExpectedException"><see langword="true"/> when an expected-exception attribute is present.</param>
    /// <returns><see langword="true"/> when one of the method's attributes is a resolved test marker.</returns>
    private static bool CarriesTestMarker(IMethodSymbol symbol, TestFrameworkFacts facts, out bool hasExpectedException)
    {
        var isTest = false;
        hasExpectedException = false;
        var attributes = symbol.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            var attributeClass = attributes[i].AttributeClass;
            if (attributeClass is null)
            {
                continue;
            }

            if (!isTest && MatchesAnyMarker(attributeClass, facts.Markers))
            {
                isTest = true;
            }

            if (!hasExpectedException
                && facts.ExpectedException is not null
                && DerivesFromOrEquals(attributeClass, facts.ExpectedException))
            {
                hasExpectedException = true;
            }
        }

        return isTest;
    }

    /// <summary>Returns whether an attribute type equals one of the resolved markers.</summary>
    /// <param name="attributeClass">The bound attribute type.</param>
    /// <param name="markers">The resolved marker types.</param>
    /// <returns><see langword="true"/> on an identity match.</returns>
    private static bool MatchesAnyMarker(INamedTypeSymbol attributeClass, INamedTypeSymbol[] markers)
    {
        for (var i = 0; i < markers.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(attributeClass, markers[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type equals or derives from a target type.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="target">The target base type.</param>
    /// <returns><see langword="true"/> when <paramref name="target"/> appears in the type's own chain.</returns>
    private static bool DerivesFromOrEquals(INamedTypeSymbol type, INamedTypeSymbol target)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Walks a test body for any node the rule cannot prove is a non-verifying platform operation.</summary>
    /// <param name="body">The method's block or expression body.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the body might verify, so the method must not be reported.</returns>
    private static bool BodyMightVerify(SyntaxNode body, SemanticModel model, CancellationToken cancellationToken)
    {
        var scan = new VerificationScan { Model = model, CancellationToken = cancellationToken };
        DescendantTraversalHelper.VisitDescendants<SyntaxNode, VerificationScan>(body, ref scan, VisitBodyNode);
        return scan.MightVerify;
    }

    /// <summary>Flags the first node that could verify and stops the walk.</summary>
    /// <param name="node">The node being visited.</param>
    /// <param name="scan">The scan state.</param>
    /// <returns><see langword="false"/> once a possibly-verifying node is found, which stops the walk.</returns>
    private static bool VisitBodyNode(SyntaxNode node, ref VerificationScan scan)
    {
        switch (node)
        {
            case ThrowStatementSyntax:
            case ThrowExpressionSyntax:
            {
                scan.MightVerify = true;
                return false;
            }

            case InvocationExpressionSyntax:
            case ObjectCreationExpressionSyntax:
            case ImplicitObjectCreationExpressionSyntax:
            {
                if (IsPlatformNonVerifyingCall(node, ref scan))
                {
                    return true;
                }

                scan.MightVerify = true;
                return false;
            }

            default:
                return true;
        }
    }

    /// <summary>Returns whether a call or object creation binds to a non-verifying platform (BCL) method.</summary>
    /// <param name="node">The invocation or object-creation node.</param>
    /// <param name="scan">The scan state carrying the semantic model.</param>
    /// <returns><see langword="true"/> only for a resolved platform method that is not an in-BCL verification helper.</returns>
    private static bool IsPlatformNonVerifyingCall(SyntaxNode node, ref VerificationScan scan)
        => scan.Model.GetSymbolInfo(node, scan.CancellationToken).Symbol is IMethodSymbol method
            && IsPlatformAssembly(method.ContainingAssembly)
            && !IsBclVerificationType(method.ContainingType);

    /// <summary>Returns whether an assembly is a platform (BCL) assembly.</summary>
    /// <param name="assembly">The method's containing assembly.</param>
    /// <returns><see langword="true"/> for mscorlib, netstandard, System.Private.CoreLib, or a System* assembly.</returns>
    private static bool IsPlatformAssembly(IAssemblySymbol? assembly)
    {
        if (assembly is null)
        {
            return false;
        }

        var name = assembly.Name;
        return name is "mscorlib" or "netstandard" or "System.Private.CoreLib" or "System"
            || name.StartsWith("System.", StringComparison.Ordinal);
    }

    /// <summary>Returns whether a type is one of the in-BCL verification helpers a test may legitimately assert with.</summary>
    /// <param name="type">The call's containing type.</param>
    /// <returns><see langword="true"/> for <c>System.Diagnostics.Debug</c>/<c>Trace</c> or <c>System.Diagnostics.Contracts.Contract</c>.</returns>
    private static bool IsBclVerificationType(INamedTypeSymbol? type) => type switch
    {
        { Name: "Debug" or "Trace" } => IsSystemDiagnostics(type.ContainingNamespace),
        { Name: "Contract" } => IsSystemDiagnosticsContracts(type.ContainingNamespace),
        _ => false,
    };

    /// <summary>Returns whether a namespace is <c>System.Diagnostics</c>.</summary>
    /// <param name="ns">The namespace to test.</param>
    /// <returns><see langword="true"/> for the <c>System.Diagnostics</c> namespace.</returns>
    private static bool IsSystemDiagnostics(INamespaceSymbol ns)
        => ns is { Name: "Diagnostics", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } };

    /// <summary>Returns whether a namespace is <c>System.Diagnostics.Contracts</c>.</summary>
    /// <param name="ns">The namespace to test.</param>
    /// <returns><see langword="true"/> for the <c>System.Diagnostics.Contracts</c> namespace.</returns>
    private static bool IsSystemDiagnosticsContracts(INamespaceSymbol ns)
        => ns is { Name: "Contracts", ContainingNamespace: { Name: "Diagnostics", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } };

    /// <summary>The resolved facts one compilation needs to find a test that verifies nothing.</summary>
    /// <param name="Markers">The referenced frameworks' test-method marker attributes.</param>
    /// <param name="ExpectedException">MSTest's expected-exception base (or concrete) type, or <see langword="null"/> when absent.</param>
    private readonly record struct TestFrameworkFacts(INamedTypeSymbol[] Markers, INamedTypeSymbol? ExpectedException);

    /// <summary>The state threaded through a test body's verification scan.</summary>
    private record struct VerificationScan
    {
        /// <summary>Gets or sets the semantic model used to bind calls in the body.</summary>
        public SemanticModel Model { get; set; }

        /// <summary>Gets or sets the cancellation token.</summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>Gets or sets a value indicating whether the body might verify something.</summary>
        public bool MightVerify { get; set; }
    }
}
