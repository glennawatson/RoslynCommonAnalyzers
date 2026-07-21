// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a lambda's single-<c>return</c> block body as an expression body (SST2257):
/// <c>x =&gt; { return expr; }</c> becomes <c>x =&gt; expr</c>. The returned expression and the body's trailing
/// trivia carry through, and the arrow keeps a single trailing space.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2257SimplifyLambdaBodyCodeFixProvider))]
[Shared]
public sealed class Sst2257SimplifyLambdaBodyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.SimplifyLambdaBody.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use an expression body", nameof(Sst2257SimplifyLambdaBodyCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported lambda and rewrites its block body as an expression body.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var lambda = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<LambdaExpressionSyntax>();
        if (lambda is null || !Sst2257SimplifyLambdaBodyAnalyzer.TryGetReturnExpression(lambda, out var expression))
        {
            return null;
        }

        var body = expression.WithoutLeadingTrivia().WithTrailingTrivia(lambda.Body.GetTrailingTrivia());
        var rewritten = lambda
            .WithArrowToken(lambda.ArrowToken.WithTrailingTrivia(SyntaxFactory.Space))
            .WithBody(body);

        return new NodeReplacement(lambda, rewritten);
    }
}
