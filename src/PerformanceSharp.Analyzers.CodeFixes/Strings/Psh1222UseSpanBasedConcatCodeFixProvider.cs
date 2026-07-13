// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported concatenation (PSH1222) onto the span overloads of <c>string.Concat</c>: the
/// <c>Substring</c> arguments become <c>AsSpan</c> with their slice arguments untouched, and the
/// remaining string arguments gain a free <c>AsSpan()</c>.
/// </summary>
/// <remarks>
/// Every argument has to move, because <c>string.Concat</c> has no overload that mixes a string with a
/// span. That is not a compromise: <c>AsSpan()</c> on a string allocates nothing and copies nothing,
/// and the result is one copy instead of two. The rewritten call is bound before it is offered.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1222UseSpanBasedConcatCodeFixProvider))]
[Shared]
public sealed class Psh1222UseSpanBasedConcatCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseSpanBasedConcat.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Concatenate the spans",
            nameof(Psh1222UseSpanBasedConcatCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported concatenation and builds its all-span rewrite.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not InvocationExpressionSyntax invocation
            || !Psh1222UseSpanBasedConcatAnalyzer.IsConcatShape(invocation))
        {
            return null;
        }

        var rewritten = Psh1222UseSpanBasedConcatAnalyzer.BuildSpanConcat(invocation);
        return BindsToSpanConcat(model, invocation.SpanStart, rewritten)
            ? new NodeReplacement(invocation, rewritten.WithTriviaFrom(invocation))
            : null;
    }

    /// <summary>Confirms the all-span rewrite still binds to a span <c>string.Concat</c> overload.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The original call's position.</param>
    /// <param name="rewritten">The rewritten call.</param>
    /// <returns><see langword="true"/> when the fix compiles.</returns>
    private static bool BindsToSpanConcat(SemanticModel model, int position, InvocationExpressionSyntax rewritten)
    {
        var symbol = model.GetSpeculativeSymbolInfo(position, rewritten, SpeculativeBindingOption.BindAsExpression).Symbol;
        return symbol is IMethodSymbol
        {
            IsStatic: true,
            Name: Psh1222UseSpanBasedConcatAnalyzer.ConcatMethodName,
            ReturnType.SpecialType: SpecialType.System_String,
            ContainingType.SpecialType: SpecialType.System_String,
        };
    }
}
