// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a route template that uses a backslash as a path separator (SST2700) so every backslash becomes a
/// forward slash. The corrected value is re-emitted as an ordinary string literal, which never needs the verbatim
/// or raw form once the backslashes are gone, and the original literal's trivia is preserved.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2700RouteTemplateBackslashCodeFixProvider))]
[Shared]
public sealed class Sst2700RouteTemplateBackslashCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(FrameworksRules.RouteTemplateBackslash.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Replace the backslash with a forward slash",
            nameof(Sst2700RouteTemplateBackslashCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported route-template literal and swaps its backslashes for forward slashes.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape is not a backslash-bearing string literal.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        // The reported span equals the literal's span, which also matches the enclosing attribute argument
        // for a positional template; take the innermost node on that tie and unwrap the argument if needed.
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (ResolveTemplateLiteral(node) is not { } literal)
        {
            return null;
        }

        var corrected = literal.Token.ValueText.Replace('\\', '/');
        var replacement = SyntaxFactory
            .LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(corrected))
            .WithTriviaFrom(literal);

        return new NodeReplacement(literal, replacement);
    }

    /// <summary>Resolves the route-template string literal from the reported node or its enclosing attribute argument.</summary>
    /// <param name="node">The innermost node at the reported span.</param>
    /// <returns>The string literal to rewrite, or <see langword="null"/> when the shape no longer matches.</returns>
    private static LiteralExpressionSyntax? ResolveTemplateLiteral(SyntaxNode node)
        => node switch
        {
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } matched => matched,
            AttributeArgumentSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } argument } => argument,
            _ => null,
        };
}
