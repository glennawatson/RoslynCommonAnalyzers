// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

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
public sealed class Psh1315NoBlockingWaitCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The awaitable that completes when every task does.</summary>
    private const string WhenAllMethodName = "WhenAll";

    /// <summary>The awaitable that completes when one task does.</summary>
    private const string WhenAnyMethodName = "WhenAny";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.NoBlockingWait.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Await instead of blocking", nameof(Psh1315NoBlockingWaitCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces a reported blocking wait with its awaited form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="blocking">The blocking expression to rewrite.</param>
    /// <returns>The updated document, or the original when the wait cannot be awaited here.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SemanticModel model, ExpressionSyntax blocking)
        => TryGetReplacement(model, blocking) is { } replacement
            ? document.WithSyntaxRoot(root.ReplaceNode(blocking, replacement))
            : document;

    /// <summary>Resolves the reported blocking wait and builds its awaited replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no fix can be offered.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is ExpressionSyntax blocking
            && TryGetReplacement(model, blocking) is { } replacement
            ? new NodeReplacement(blocking, replacement)
            : null;

    /// <summary>Builds the awaited replacement for a reported blocking wait.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="blocking">The blocking expression to rewrite.</param>
    /// <returns>The replacement expression, or <see langword="null"/> when awaiting here would not compile or would not mean the same thing.</returns>
    private static ExpressionSyntax? TryGetReplacement(SemanticModel model, ExpressionSyntax blocking)
    {
        if (!Psh1303NoThreadSleepInAsyncAnalyzer.IsInAsyncFunction(blocking)
            || !AwaitPlacement.IsLegalAt(blocking)
            || AsyncSiblingResolver.TaskTypes.Create(model.Compilation) is not { } tasks
            || BlockingWait.TryMatch(blocking, model, tasks, CancellationToken.None) is not { AwaitIsEquivalent: true } site)
        {
            return null;
        }

        if (site.Kind == BlockingWait.Kind.SingleTask)
        {
            return AwaitOf(site.Awaited, blocking);
        }

        return TryGetCombinatorReplacement(model, (InvocationExpressionSyntax)blocking, site.Kind);
    }

    /// <summary>Builds <c>await Task.WhenAll(…)</c> or <c>await Task.WhenAny(…)</c> for a reported combinator.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="blocking">The <c>Task.WaitAll</c> or <c>Task.WaitAny</c> invocation.</param>
    /// <param name="kind">Which combinator was reported.</param>
    /// <returns>The awaited replacement, or <see langword="null"/> when the rewritten call does not bind.</returns>
    /// <remarks>
    /// The receiver is left exactly as it was written — a qualified or aliased <c>Task</c> stays
    /// qualified or aliased — and only the method name moves. The rewritten call is then bound
    /// speculatively: a framework whose <c>WhenAll</c> has no overload for the arguments already
    /// written gets no fix rather than one that does not compile.
    /// </remarks>
    private static ExpressionSyntax? TryGetCombinatorReplacement(SemanticModel model, InvocationExpressionSyntax blocking, BlockingWait.Kind kind)
    {
        var access = (MemberAccessExpressionSyntax)blocking.Expression;
        var whenName = kind == BlockingWait.Kind.WaitAll ? WhenAllMethodName : WhenAnyMethodName;
        var candidate = blocking.WithExpression(
            access.WithName(SyntaxFactory.IdentifierName(whenName).WithTriviaFrom(access.Name)));

        var speculative = model.GetSpeculativeSymbolInfo(blocking.SpanStart, candidate, SpeculativeBindingOption.BindAsExpression);
        if (speculative.Symbol is not IMethodSymbol)
        {
            return null;
        }

        return AwaitOf(candidate, blocking);
    }

    /// <summary>Wraps an expression in an <c>await</c>, parenthesized where the surrounding expression needs it.</summary>
    /// <param name="awaited">The expression to await.</param>
    /// <param name="blocking">The blocking expression being replaced, whose trivia and position the result takes on.</param>
    /// <returns>The awaited expression.</returns>
    private static ExpressionSyntax AwaitOf(ExpressionSyntax awaited, ExpressionSyntax blocking)
    {
        ExpressionSyntax result = SyntaxFactory.AwaitExpression(
            SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            awaited.WithoutTrivia());
        if (NeedsParentheses(blocking))
        {
            result = SyntaxFactory.ParenthesizedExpression(result);
        }

        return result.WithTriviaFrom(blocking).WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>Returns whether the surrounding expression binds tighter than <c>await</c>, so the result needs parentheses.</summary>
    /// <param name="blocking">The expression being replaced.</param>
    /// <returns><see langword="true"/> when the replacement must be parenthesized to keep its meaning.</returns>
    private static bool NeedsParentheses(ExpressionSyntax blocking)
        => blocking.Parent switch
        {
            MemberAccessExpressionSyntax access => access.Expression == blocking,
            ElementAccessExpressionSyntax element => element.Expression == blocking,
            InvocationExpressionSyntax invocation => invocation.Expression == blocking,
            ConditionalAccessExpressionSyntax conditional => conditional.Expression == blocking,
            PostfixUnaryExpressionSyntax => true,
            _ => false,
        };
}
