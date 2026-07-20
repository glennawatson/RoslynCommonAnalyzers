// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a fluent assertion that is started but never completed (SST2508): an expression statement
/// whose whole expression is a bare <c>value.Should()</c> invocation with no check chained after it. A
/// fluent assertion only verifies once a check — <c>Be</c>, <c>Contain</c>, <c>BeTrue</c>, and the like —
/// is chained onto the subject; a <c>value.Should();</c> statement stops at the subject, so it compiles,
/// runs, and passes while checking nothing.
/// </summary>
/// <remarks>
/// <para>
/// The whole rule is gated at compilation start on a fluent-assertion library being referenced — the
/// <c>AssertionExtensions</c> type that hosts the <c>Should()</c> extension methods, under either the
/// <c>FluentAssertions</c> or the <c>AwesomeAssertions</c> namespace, must resolve. A project that
/// references neither registers no per-statement callback and pays nothing.
/// </para>
/// <para>
/// The clean path is syntax only: the statement's expression must be an invocation whose invoked member
/// is named <c>Should</c> — which, being the top-level invocation, also proves nothing is chained after
/// it — before anything binds. Only then is the call bound to confirm its return type is a fluent-assertion
/// subject type (its type's root namespace is <c>FluentAssertions</c> or <c>AwesomeAssertions</c>), which
/// keeps an unrelated <c>Should()</c> from any other library silent. A completed
/// <c>value.Should().Be(1)</c> is never reported, because its statement expression is the chained
/// <c>Be</c> invocation, not <c>Should</c>. No code fix is offered: only the test's author knows which
/// check the subject was meant to carry.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2508IncompleteAssertionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The extension-method name that begins a fluent assertion by naming its subject.</summary>
    private const string ShouldMethodName = "Should";

    /// <summary>The root namespace of the original fluent-assertion library.</summary>
    private const string FluentAssertionsNamespace = "FluentAssertions";

    /// <summary>The root namespace of the maintained fork of the fluent-assertion library.</summary>
    private const string AwesomeAssertionsNamespace = "AwesomeAssertions";

    /// <summary>The metadata names of the static class that hosts the <c>Should()</c> extension methods.</summary>
    private static readonly string[] AssertionExtensionsMetadataNames =
    [
        "FluentAssertions.AssertionExtensions",
        "AwesomeAssertions.AssertionExtensions",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(TestingRules.IncompleteAssertion);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the per-statement callback only when a fluent-assertion library is referenced.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (!IsFluentAssertionLibraryPresent(context.Compilation))
        {
            return;
        }

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ExpressionStatement);
    }

    /// <summary>Returns whether the compilation references a fluent-assertion library.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <returns><see langword="true"/> when a hosting <c>AssertionExtensions</c> type resolves.</returns>
    private static bool IsFluentAssertionLibraryPresent(Compilation compilation)
    {
        for (var i = 0; i < AssertionExtensionsMetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(AssertionExtensionsMetadataNames[i]) is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Analyzes one expression statement for a bare, never-completed <c>Should()</c> assertion.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var statement = (ExpressionStatementSyntax)context.Node;
        if (statement.Expression is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Name.Identifier.ValueText != ShouldMethodName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsFluentAssertionSubject(method.ReturnType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(TestingRules.IncompleteAssertion, invocation.GetLocation()));
    }

    /// <summary>Returns whether a type is a fluent-assertion subject, by its root namespace.</summary>
    /// <param name="type">The bound return type of the <c>Should()</c> call.</param>
    /// <returns><see langword="true"/> when the type's root namespace is a fluent-assertion library's.</returns>
    private static bool IsFluentAssertionSubject(ITypeSymbol type)
    {
        if (type.ContainingNamespace is not { IsGlobalNamespace: false } rootNamespace)
        {
            return false;
        }

        while (rootNamespace.ContainingNamespace is { IsGlobalNamespace: false } parent)
        {
            rootNamespace = parent;
        }

        return rootNamespace.Name is FluentAssertionsNamespace or AwesomeAssertionsNamespace;
    }
}
