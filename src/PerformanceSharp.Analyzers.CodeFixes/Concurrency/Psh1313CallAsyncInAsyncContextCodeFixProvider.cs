// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported blocking call to an awaited one (PSH1313). <c>task.Result</c>,
/// <c>task.Wait()</c>, and <c>task.GetAwaiter().GetResult()</c> all become <c>await task</c>,
/// while a synchronous call with an async sibling becomes <c>await x.FooAsync(...)</c> carrying
/// its arguments over unchanged. The result is parenthesized wherever the surrounding expression
/// binds tighter than <c>await</c> — <c>task.Result.Length</c> becomes
/// <c>(await task).Length</c>, not <c>await task.Length</c>.
/// <para>
/// The rewritten sibling call is speculatively bound before the fix is offered, so a replacement
/// that would not compile is never suggested; the enclosing function is re-checked for
/// <c>async</c> so the inserted <c>await</c> is always legal where it lands.
/// </para>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1313CallAsyncInAsyncContextCodeFixProvider))]
[Shared]
public sealed class Psh1313CallAsyncInAsyncContextCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.CallAsyncInAsyncContext.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Await instead of blocking", nameof(Psh1313CallAsyncInAsyncContextCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces a reported blocking call with its awaited form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="blocking">The blocking expression to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SemanticModel model, ExpressionSyntax blocking)
        => TryGetReplacement(model, blocking, out var replacement)
            ? document.WithSyntaxRoot(root.ReplaceNode(blocking, replacement!))
            : document;

    /// <summary>Resolves the reported blocking call and builds its awaited replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is ExpressionSyntax blocking
            && TryGetReplacement(model, blocking, out var replacement)
            ? new NodeReplacement(blocking, replacement!)
            : null;

    /// <summary>Builds the awaited replacement for a reported blocking call.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="blocking">The blocking expression to rewrite.</param>
    /// <param name="replacement">The replacement expression when one could be built.</param>
    /// <returns><see langword="true"/> when a replacement was built.</returns>
    private static bool TryGetReplacement(SemanticModel model, ExpressionSyntax blocking, out ExpressionSyntax? replacement)
    {
        replacement = null;
        if (!Psh1303NoThreadSleepInAsyncAnalyzer.IsInAsyncFunction(blocking)
            || TryGetAwaitedOperand(model, blocking) is not { } operand)
        {
            return false;
        }

        ExpressionSyntax result = SyntaxFactory.AwaitExpression(
            SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            operand.WithoutTrivia());
        if (NeedsParentheses(blocking))
        {
            result = SyntaxFactory.ParenthesizedExpression(result);
        }

        replacement = result.WithTriviaFrom(blocking).WithAdditionalAnnotations(Formatter.Annotation);
        return true;
    }

    /// <summary>Resolves what the awaited replacement should await: the blocked task, or the async sibling call.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="blocking">The blocking expression.</param>
    /// <returns>The expression to await, or <see langword="null"/> when the shape no longer matches.</returns>
    private static ExpressionSyntax? TryGetAwaitedOperand(SemanticModel model, ExpressionSyntax blocking)
    {
        if (blocking is MemberAccessExpressionSyntax { Name.Identifier.ValueText: Psh1313CallAsyncInAsyncContextAnalyzer.ResultPropertyName } result)
        {
            return result.Expression;
        }

        if (blocking is not InvocationExpressionSyntax invocation)
        {
            return null;
        }

        if (Psh1313CallAsyncInAsyncContextAnalyzer.TryGetAwaiterChainReceiver(invocation) is { } awaited)
        {
            return awaited;
        }

        return invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: Psh1313CallAsyncInAsyncContextAnalyzer.WaitMethodName } wait
            && invocation.ArgumentList.Arguments.Count == 0
            ? wait.Expression
            : TryBuildSiblingCall(model, invocation);
    }

    /// <summary>Builds the async sibling call for a reported synchronous invocation, and proves it binds.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The synchronous invocation.</param>
    /// <returns>The sibling invocation, or <see langword="null"/> when it cannot be resolved or bound.</returns>
    private static InvocationExpressionSyntax? TryBuildSiblingCall(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        if (AsyncSiblingResolver.TaskTypes.Create(model.Compilation) is not { } tasks
            || model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol sync
            || AsyncSiblingResolver.TryResolveAsyncSibling(sync, tasks) is not { } sibling)
        {
            return null;
        }

        var name = SyntaxFactory.IdentifierName(sibling.Name);
        ExpressionSyntax callee = invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.WithName(name),
            IdentifierNameSyntax => name,
            _ => invocation.Expression,
        };

        var candidate = invocation.WithExpression(callee).WithoutTrivia();
        return BindsToSibling(model, invocation.SpanStart, candidate, sibling) ? candidate : null;
    }

    /// <summary>Speculatively binds the rewritten call and confirms it resolves to the sibling that was resolved.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The original call's position, used as the speculative binding context.</param>
    /// <param name="candidate">The rewritten sibling invocation.</param>
    /// <param name="sibling">The sibling the analyzer resolved.</param>
    /// <returns><see langword="true"/> when the replacement binds to that sibling.</returns>
    private static bool BindsToSibling(SemanticModel model, int position, InvocationExpressionSyntax candidate, IMethodSymbol sibling)
        => model.GetSpeculativeSymbolInfo(position, candidate, SpeculativeBindingOption.BindAsExpression).Symbol
                is IMethodSymbol bound
            && SymbolEqualityComparer.Default.Equals(bound.OriginalDefinition, sibling.OriginalDefinition);

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
