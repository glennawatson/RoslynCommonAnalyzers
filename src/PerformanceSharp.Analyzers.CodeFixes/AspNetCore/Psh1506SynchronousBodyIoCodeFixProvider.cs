// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported synchronous body read or write to its awaited async overload (PSH1506):
/// <c>ctx.Request.Body.Read(buffer, 0, n)</c> becomes
/// <c>await ctx.Request.Body.ReadAsync(buffer, 0, n)</c>, and
/// <c>new StreamReader(ctx.Request.Body).ReadToEnd()</c> becomes
/// <c>await new StreamReader(ctx.Request.Body).ReadToEndAsync()</c>, carrying the arguments over
/// unchanged. The result is parenthesized wherever the surrounding expression binds tighter than
/// <c>await</c>.
/// <para>
/// The fix is offered only where the inserted <c>await</c> would compile — the enclosing method,
/// local function, or lambda must already be <c>async</c> — and only when an async sibling that
/// awaits to the synchronous call's own result and accepts the very arguments already written binds
/// speculatively. A call such as <c>ReadByte()</c>, whose async form has a different signature, and
/// a call in a synchronous method, are reported by the analyzer without a fix rather than rewritten
/// into something that would not build.
/// </para>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1506SynchronousBodyIoCodeFixProvider))]
[Shared]
public sealed class Psh1506SynchronousBodyIoCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AspNetCoreRules.SynchronousBodyIo.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Await the async overload", nameof(Psh1506SynchronousBodyIoCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported synchronous call and builds its awaited replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no fix can be offered.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation
            && TryGetReplacement(model, invocation) is { } replacement
            ? new NodeReplacement(invocation, replacement)
            : null;

    /// <summary>Builds the awaited sibling call for a reported synchronous body I/O invocation.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The synchronous call to rewrite.</param>
    /// <returns>The replacement expression, or <see langword="null"/> when awaiting here would not compile or the sibling does not fit.</returns>
    private static ExpressionSyntax? TryGetReplacement(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        if (!Psh1303NoThreadSleepInAsyncAnalyzer.IsInAsyncFunction(invocation)
            || TryBuildSiblingCall(model, invocation) is not { } sibling)
        {
            return null;
        }

        return AwaitExpressionRewrite.WrapInAwait(sibling, invocation);
    }

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

        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || AsyncSiblingResolver.TaskTypes.Create(model.Compilation) is not { } tasks
            || model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol sync
            || AsyncSiblingResolver.TryResolveAsyncSibling(sync, tasks) is not { } sibling)
        {
            return null;
        }

        var candidate = invocation
            .WithExpression(access.WithName(SyntaxFactory.IdentifierName(sibling.Name)))
            .WithoutTrivia();
        return BindsToSibling(model, invocation.SpanStart, candidate, sibling) ? candidate : null;
    }

    /// <summary>Speculatively binds the rewritten call and confirms it resolves to the sibling that was resolved.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The original call's position, used as the speculative binding context.</param>
    /// <param name="candidate">The rewritten sibling invocation.</param>
    /// <param name="sibling">The sibling the resolver produced.</param>
    /// <returns><see langword="true"/> when the replacement binds to that sibling.</returns>
    private static bool BindsToSibling(SemanticModel model, int position, InvocationExpressionSyntax candidate, IMethodSymbol sibling)
        => model.GetSpeculativeSymbolInfo(position, candidate, SpeculativeBindingOption.BindAsExpression).Symbol is IMethodSymbol bound
            && SymbolEqualityComparer.Default.Equals(bound.OriginalDefinition, sibling.OriginalDefinition);
}
