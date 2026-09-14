// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported blocking wait into an awaited one (PSH1315). <c>task.Result</c>,
/// <c>task.Wait()</c>, and <c>task.GetAwaiter().GetResult()</c> all become <c>await task</c>, and a
/// <c>ConfigureAwait</c> in front of the awaiter is carried across so
/// <c>t.ConfigureAwait(false).GetAwaiter().GetResult()</c> becomes
/// <c>await t.ConfigureAwait(false)</c>. <c>Task.WaitAll(a, b)</c> becomes
/// <c>await Task.WhenAll(a, b)</c> and <c>Task.WaitAny(a, b)</c> becomes
/// <c>await Task.WhenAny(a, b)</c>, with the rewritten call bound speculatively first so an
/// overload that does not exist in the analyzed framework is never offered. The result is
/// parenthesized wherever the surrounding expression binds tighter than <c>await</c> —
/// <c>task.Result.Length</c> becomes <c>(await task).Length</c>, not <c>await task.Length</c>.
/// <para>
/// The fix is offered only where the <c>await</c> would compile: the enclosing function must
/// already be <c>async</c>, and the position must not be one C# forbids awaiting in (see
/// <see cref="AwaitPlacement"/>). A wait in a synchronous method is reported without a fix rather
/// than rewritten into something that does not build — the author has to decide how far up the
/// call chain <c>async</c> goes, and no code action can decide that for them.
/// </para>
/// <para>
/// Four shapes are reported and never rewritten, because no rewrite means the same thing.
/// <c>Wait(timeout)</c> and <c>Wait(cancellationToken)</c>, and the <c>WaitAll</c> overloads that
/// take one of those, give up on the wait in a way <c>await</c> does not. A <c>WaitAny</c> whose
/// result is used returns the *index* of the task that finished, where <c>WhenAny</c> returns the
/// task itself. And <c>RunSynchronously</c> starts a cold task on this thread: awaiting a task
/// nothing ever started waits forever, so only its author can say what it should have been.
/// </para>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1315NoBlockingWaitCodeFixProvider))]
[Shared]
public sealed class Psh1315NoBlockingWaitCodeFixProvider : CodeFixProvider
{
    /// <summary>The awaitable that completes when every task does.</summary>
    private const string WhenAllMethodName = "WhenAll";

    /// <summary>The awaitable that completes when one task does.</summary>
    private const string WhenAnyMethodName = "WhenAny";

    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.NoBlockingWait.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Await instead of blocking", nameof(Psh1315NoBlockingWaitCodeFixProvider), CanRewrite, TryRewrite);

    /// <summary>Replaces a reported blocking wait with its awaited form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="blocking">The blocking expression to rewrite.</param>
    /// <returns>The updated document, or the original when the wait cannot be awaited here.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SemanticModel model, ExpressionSyntax blocking) =>
        TryGetReplacement(model, blocking) is { } replacement
            ? document.WithSyntaxRoot(root.ReplaceNode(blocking, replacement))
            : document;

    /// <summary>Checks applicability without building the awaited replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the blocking wait can be rewritten.</returns>
    /// <remarks>Combinators still require a speculative call to validate overload resolution.</remarks>
    private static bool CanRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is ExpressionSyntax blocking
            && TryGetSite(model, blocking) is { } site
            && (site.Kind == BlockingWait.Kind.SingleTask
                || TryGetCombinatorCall(model, (InvocationExpressionSyntax)blocking, site.Kind) is not null);

    /// <summary>Resolves the reported blocking wait and builds its awaited replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no fix can be offered.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is ExpressionSyntax blocking
            && TryGetReplacement(model, blocking) is { } replacement
            ? new NodeReplacement(blocking, replacement)
            : null;

    /// <summary>Builds the awaited replacement for a reported blocking wait.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="blocking">The blocking expression to rewrite.</param>
    /// <returns>The replacement expression, or <see langword="null"/> when awaiting here would not compile or would not mean the same thing.</returns>
    private static ExpressionSyntax? TryGetReplacement(SemanticModel model, ExpressionSyntax blocking)
    {
        if (TryGetSite(model, blocking) is not { } site)
        {
            return null;
        }

        if (site.Kind == BlockingWait.Kind.SingleTask)
        {
            return AwaitExpressionRewrite.WrapInAwait(site.Awaited, blocking);
        }

        return TryGetCombinatorCall(model, (InvocationExpressionSyntax)blocking, site.Kind) is { } candidate
            ? AwaitExpressionRewrite.WrapInAwait(candidate, blocking)
            : null;
    }

    /// <summary>Matches a blocking wait whose equivalent await is legal at this position.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="blocking">The blocking expression.</param>
    /// <returns>The matched wait, or null when it cannot be awaited here.</returns>
    private static BlockingWait.Site? TryGetSite(SemanticModel model, ExpressionSyntax blocking) =>
        Psh1303NoThreadSleepInAsyncAnalyzer.IsInAsyncFunction(blocking)
            && AwaitPlacement.IsLegalAt(blocking)
            && AsyncSiblingResolver.TaskTypes.Create(model.Compilation) is { } tasks
            && BlockingWait.TryMatch(blocking, model, tasks, CancellationToken.None) is { AwaitIsEquivalent: true } site
            ? site
            : null;

    /// <summary>Builds and binds <c>Task.WhenAll(…)</c> or <c>Task.WhenAny(…)</c> for a reported combinator.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="blocking">The <c>Task.WaitAll</c> or <c>Task.WaitAny</c> invocation.</param>
    /// <param name="kind">Which combinator was reported.</param>
    /// <returns>The replacement call, or <see langword="null"/> when it does not bind.</returns>
    /// <remarks>
    /// The receiver is left exactly as it was written — a qualified or aliased <c>Task</c> stays
    /// qualified or aliased — and only the method name moves. The rewritten call is then bound
    /// speculatively: a framework whose <c>WhenAll</c> has no overload for the arguments already
    /// written gets no fix rather than one that does not compile.
    /// </remarks>
    private static InvocationExpressionSyntax? TryGetCombinatorCall(SemanticModel model, InvocationExpressionSyntax blocking, BlockingWait.Kind kind)
    {
        var access = (MemberAccessExpressionSyntax)blocking.Expression;
        var whenName = kind == BlockingWait.Kind.WaitAll ? WhenAllMethodName : WhenAnyMethodName;
        var candidate = blocking.Update(
            access.Update(
                access.Expression,
                access.OperatorToken,
                SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(access.Name.GetLeadingTrivia(), whenName, access.Name.GetTrailingTrivia()))),
            blocking.ArgumentList);

        var speculative = model.GetSpeculativeSymbolInfo(blocking.SpanStart, candidate, SpeculativeBindingOption.BindAsExpression);
        return speculative.Symbol is IMethodSymbol ? candidate : null;
    }
}
