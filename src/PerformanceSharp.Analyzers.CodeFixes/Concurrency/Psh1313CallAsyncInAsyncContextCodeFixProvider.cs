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
public sealed class Psh1313CallAsyncInAsyncContextCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.CallAsyncInAsyncContext.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Await the async overload", nameof(Psh1313CallAsyncInAsyncContextCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces a reported synchronous call with its awaited async sibling.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="blocking">The synchronous call to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SemanticModel model, ExpressionSyntax blocking)
        => TryGetReplacement(model, blocking) is { } replacement
            ? document.WithSyntaxRoot(root.ReplaceNode(blocking, replacement))
            : document;

    /// <summary>Resolves the reported synchronous call and builds its awaited replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is ExpressionSyntax blocking
            && TryGetReplacement(model, blocking) is { } replacement
            ? new NodeReplacement(blocking, replacement)
            : null;

    /// <summary>Builds the awaited sibling call for a reported synchronous invocation.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="blocking">The synchronous call to rewrite.</param>
    /// <returns>The replacement expression, or <see langword="null"/> when the shape no longer matches.</returns>
    private static ExpressionSyntax? TryGetReplacement(SemanticModel model, ExpressionSyntax blocking)
    {
        if (!Psh1303NoThreadSleepInAsyncAnalyzer.IsInAsyncFunction(blocking)
            || blocking is not InvocationExpressionSyntax invocation
            || TryBuildSiblingCall(model, invocation) is not { } sibling)
        {
            return null;
        }

        return AwaitExpressionRewrite.WrapInAwait(sibling, blocking);
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
}
