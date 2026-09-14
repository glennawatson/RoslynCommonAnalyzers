// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported synchronous call to its awaited async sibling (PSH1313):
/// <c>x.Foo(args)</c> becomes <c>await x.FooAsync(args)</c>, carrying the arguments over
/// unchanged. The result is parenthesized wherever the surrounding expression binds tighter than
/// <c>await</c> — <c>File.ReadAllText(path).Length</c> becomes
/// <c>(await File.ReadAllTextAsync(path)).Length</c>.
/// <para>
/// The rewritten sibling call is speculatively bound before the fix is offered, so a replacement
/// that would not compile is never suggested; the enclosing function is re-checked for
/// <c>async</c> so the inserted <c>await</c> is always legal where it lands.
/// </para>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1313CallAsyncInAsyncContextCodeFixProvider))]
[Shared]
public sealed class Psh1313CallAsyncInAsyncContextCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.CallAsyncInAsyncContext.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            TryCreateTitle,
            static _ => nameof(Psh1313CallAsyncInAsyncContextCodeFixProvider),
            TryRewrite);

    /// <summary>Resolves the reported blocking call and builds the awaited async sibling that replaces it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no async sibling binds.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is ExpressionSyntax blocking
            && TryGetReplacement(model, blocking) is { } replacement
            ? new NodeReplacement(blocking, replacement)
            : null;

    /// <summary>Builds the awaited sibling call for a reported synchronous invocation.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="blocking">The synchronous call to rewrite.</param>
    /// <returns>The replacement expression, or <see langword="null"/> when the shape no longer matches.</returns>
    private static ExpressionSyntax? TryGetReplacement(SemanticModel model, ExpressionSyntax blocking) => !Psh1303NoThreadSleepInAsyncAnalyzer.IsInAsyncFunction(blocking)
            || blocking is not InvocationExpressionSyntax invocation
            || TryBuildSiblingCall(model, invocation) is not { } sibling
        ? null
        : AwaitExpressionRewrite.WrapInAwait(sibling, blocking);

    /// <summary>Words the action when the reported call sits in an async function and an async sibling binds.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The code action title, or <see langword="null"/> when no awaited sibling applies.</returns>
    private static string? TryCreateTitle(SyntaxNode root, SemanticModel model, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation
            && Psh1303NoThreadSleepInAsyncAnalyzer.IsInAsyncFunction(invocation)
            && TryBuildSiblingCall(model, invocation) is not null
            ? "Await the async overload"
            : null;

    /// <summary>Builds the async sibling call for a reported synchronous invocation, and proves it binds.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The synchronous invocation.</param>
    /// <returns>The sibling invocation, or <see langword="null"/> when it cannot be resolved or bound.</returns>
    private static InvocationExpressionSyntax? TryBuildSiblingCall(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        // A call reached through a conditional access cannot be speculatively rebound: detaching it to test the
        // async-sibling rewrite orphans its member or element binding and Roslyn's binder then dereferences null.
        // The diagnostic still reports on the `?.` form; only the automatic fix stands down there.
        if (ConditionalAccessSpeculation.ReachedThroughConditionalAccess(invocation.Expression))
        {
            return null;
        }

        if (AsyncSiblingResolver.TaskTypes.Create(model.Compilation) is not { } tasks
            || model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol sync
            || AsyncSiblingResolver.TryResolveAsyncSibling(sync, tasks) is not { } sibling)
        {
            return null;
        }

        var name = SyntaxFactory.IdentifierName(sibling.Name);
        var callee = invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.WithName(name),
            IdentifierNameSyntax => name,
            _ => invocation.Expression,
        };

        var candidate = invocation.Update(callee.WithoutLeadingTrivia(), invocation.ArgumentList.WithoutTrailingTrivia());
        return BindsToSibling(model, invocation.SpanStart, candidate, sibling) ? candidate : null;
    }

    /// <summary>Speculatively binds the rewritten call and confirms it resolves to the sibling that was resolved.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The original call's position, used as the speculative binding context.</param>
    /// <param name="candidate">The rewritten sibling invocation.</param>
    /// <param name="sibling">The sibling the analyzer resolved.</param>
    /// <returns><see langword="true"/> when the replacement binds to that sibling.</returns>
    private static bool BindsToSibling(SemanticModel model, int position, InvocationExpressionSyntax candidate, IMethodSymbol sibling) =>
        model.GetSpeculativeSymbolInfo(position, candidate, SpeculativeBindingOption.BindAsExpression).Symbol
                is IMethodSymbol bound
            && SymbolEqualityComparer.Default.Equals(bound.OriginalDefinition, sibling.OriginalDefinition);
}
