// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Swaps a single-character string argument of <c>StringBuilder.Append</c>/<c>Insert</c>
/// for the equivalent char literal (PSH1202). Escaping is handled by
/// <see cref="SyntaxFactory.Literal(char)"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1202StringBuilderAppendCharCodeFixProvider))]
[Shared]
public sealed class Psh1202StringBuilderAppendCharCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.StringBuilderAppendChar.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use the char overload", nameof(Psh1202StringBuilderAppendCharCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported string literal with its char literal form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="literal">The reported single-character string literal.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, LiteralExpressionSyntax literal)
        => document.WithSyntaxRoot(root.ReplaceNode(literal, Rewrite(literal)));

    /// <summary>Resolves the reported string literal and builds its char literal replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => TryGetLiteral(root, diagnostic, out var literal)
            ? new NodeReplacement(literal!, Rewrite(literal!))
            : null;

    /// <summary>Finds the reported single-character string literal for a diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="literal">The reported literal when found.</param>
    /// <returns><see langword="true"/> when the literal was found.</returns>
    private static bool TryGetLiteral(SyntaxNode root, Diagnostic diagnostic, out LiteralExpressionSyntax? literal)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is ExpressionSyntax expression
            && StringLiteralHelper.TryGetSingleCharacterLiteral(expression, out literal, out _))
        {
            return true;
        }

        literal = null;
        return false;
    }

    /// <summary>Builds the char literal that replaces the string literal.</summary>
    /// <param name="literal">The reported single-character string literal.</param>
    /// <returns>The replacement char literal.</returns>
    private static LiteralExpressionSyntax Rewrite(LiteralExpressionSyntax literal)
        => SyntaxFactory.LiteralExpression(
            SyntaxKind.CharacterLiteralExpression,
            SyntaxFactory.Literal(literal.Token.ValueText[0])).WithTriviaFrom(literal);
}
