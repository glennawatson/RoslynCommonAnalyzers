// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
/// Framework markers are resolved only for syntactically eligible methods. The clean path is a syntactic prepass:
/// a method is ignored outright unless one of its attributes is spelled with a known test-attribute simple name (<c>Fact</c>,
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
    /// <summary>The name of the root namespace the in-BCL verification helpers live under.</summary>
    private const string SystemNamespaceName = "System";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(TestingRules.TestAssertsNothing);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new FrameworkTypes(compilation),
            Analyze,
            SyntaxKind.MethodDeclaration);
    }

    /// <summary>Reports one test method whose body provably verifies nothing.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="types">The compilation's deferred framework types.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, FrameworkTypes types)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!SyntaxNames.AnyAttributeNamed(method.AttributeLists, TestAttributeNames.IsTestAttributeName))
        {
            return;
        }

        var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body is null)
        {
            return;
        }

        var markers = types.GetMarkers();
        if (markers.Length == 0
            || context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is not IMethodSymbol symbol)
        {
            return;
        }

        var expectedException = types.GetExpectedException();
        var facts = new TestFrameworkFacts(markers, expectedException);
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

            if (!isTest && TypeRelations.IsOrDerivesFromAny(attributeClass, facts.Markers))
            {
                isTest = true;
            }

            if (!hasExpectedException
                && facts.ExpectedException is not null
                && TypeRelations.IsOrDerivesFrom(attributeClass, facts.ExpectedException))
            {
                hasExpectedException = true;
            }
        }

        return isTest;
    }

    /// <summary>Walks a test body for any node the rule cannot prove is a non-verifying platform operation.</summary>
    /// <param name="body">The method's block or expression body.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the body might verify, so the method must not be reported.</returns>
    private static bool BodyMightVerify(SyntaxNode body, SemanticModel model, CancellationToken cancellationToken)
    {
        var scan = new VerificationScan { Model = model, CancellationToken = cancellationToken };
        _ = DescendantTraversalHelper.VisitDescendants<SyntaxNode, VerificationScan>(body, ref scan, VisitBodyNode);
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
            case ThrowStatementSyntax or ThrowExpressionSyntax:
            {
                scan.MightVerify = true;
                return false;
            }

            case InvocationExpressionSyntax or ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax:
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
    private static bool IsPlatformNonVerifyingCall(SyntaxNode node, ref VerificationScan scan) =>
        scan.Model.GetSymbolInfo(node, scan.CancellationToken).Symbol is IMethodSymbol method
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
    private static bool IsSystemDiagnostics(INamespaceSymbol ns) =>
        ns is { Name: "Diagnostics", ContainingNamespace: { Name: SystemNamespaceName, ContainingNamespace.IsGlobalNamespace: true } };

    /// <summary>Returns whether a namespace is <c>System.Diagnostics.Contracts</c>.</summary>
    /// <param name="ns">The namespace to test.</param>
    /// <returns><see langword="true"/> for the <c>System.Diagnostics.Contracts</c> namespace.</returns>
    private static bool IsSystemDiagnosticsContracts(INamespaceSymbol ns) =>
        ns is { Name: "Contracts", ContainingNamespace: { Name: "Diagnostics", ContainingNamespace: { Name: SystemNamespaceName, ContainingNamespace.IsGlobalNamespace: true } } };

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

    /// <summary>Resolves framework types once per compilation, after their candidate checks pass.</summary>
    /// <param name="compilation">The compilation whose types are resolved.</param>
    private sealed class FrameworkTypes(Compilation compilation)
    {
        /// <summary>The metadata name of MSTest's expected-exception attribute base type.</summary>
        private const string ExpectedExceptionBaseMetadataName =
            "Microsoft.VisualStudio.TestTools.UnitTesting.ExpectedExceptionBaseAttribute";

        /// <summary>The metadata name of MSTest's concrete expected-exception attribute.</summary>
        private const string ExpectedExceptionMetadataName =
            "Microsoft.VisualStudio.TestTools.UnitTesting.ExpectedExceptionAttribute";

        /// <summary>The metadata names of the supported frameworks' test-method marker attributes.</summary>
        private static readonly string[] TestMarkerMetadataNames = TestAttributeNames.CreateMarkerMetadataNames();

        /// <summary>The resolved test markers, including an empty result when none are present.</summary>
        private INamedTypeSymbol[]? _markers;

        /// <summary>The resolved expected-exception result, including a null entry when absent.</summary>
        private INamedTypeSymbol?[]? _expectedException;

        /// <summary>Gets the test markers, resolving them on first demand.</summary>
        /// <returns>The supported test markers, or an empty array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol[] GetMarkers() => _markers ??= MetadataTypeLookup.ResolveAll(compilation, TestMarkerMetadataNames);

        /// <summary>Gets the expected-exception type, caching an absent type too.</summary>
        /// <returns>The expected-exception base or concrete type, or null when absent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? GetExpectedException() => (_expectedException ??=
        [
            compilation.GetTypeByMetadataName(ExpectedExceptionBaseMetadataName)
                ?? compilation.GetTypeByMetadataName(ExpectedExceptionMetadataName),
        ])[0];
    }
}
