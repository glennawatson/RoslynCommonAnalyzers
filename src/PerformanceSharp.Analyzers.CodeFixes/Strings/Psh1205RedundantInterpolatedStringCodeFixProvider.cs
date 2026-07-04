// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Removes an interpolated string that does no interpolation work (PSH1205). A
/// single bare string hole is replaced by its expression, parenthesized when the
/// expression is not a primary expression; a hole-free interpolated string is
/// replaced by a plain literal with the same value, unescaping <c>{{</c>/<c>}}</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1205RedundantInterpolatedStringCodeFixProvider))]
[Shared]
public sealed class Psh1205RedundantInterpolatedStringCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.RedundantInterpolatedString.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Remove the redundant interpolation", nameof(Psh1205RedundantInterpolatedStringCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported interpolated string with its value or literal form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="interpolated">The interpolated string to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InterpolatedStringExpressionSyntax interpolated)
        => TryGetReplacement(interpolated, out var replacement)
            ? document.WithSyntaxRoot(root.ReplaceNode(interpolated, replacement!))
            : document;

    /// <summary>Resolves the reported interpolated string and builds its value or literal replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is InterpolatedStringExpressionSyntax interpolated
            && TryGetReplacement(interpolated, out var replacement)
            ? new NodeReplacement(interpolated, replacement!)
            : null;

    /// <summary>Builds the replacement for a reported interpolated string.</summary>
    /// <param name="interpolated">The interpolated string to rewrite.</param>
    /// <param name="replacement">The replacement expression when the shape is recognized.</param>
    /// <returns><see langword="true"/> when a replacement was built.</returns>
    private static bool TryGetReplacement(InterpolatedStringExpressionSyntax interpolated, out ExpressionSyntax? replacement)
    {
        if (!Psh1205RedundantInterpolatedStringAnalyzer.TryClassify(interpolated, out var singleInterpolation))
        {
            replacement = null;
            return false;
        }

        var result = singleInterpolation is not null
            ? BuildValueReplacement(singleInterpolation.Expression)
            : BuildLiteralReplacement(interpolated);

        replacement = result.WithTriviaFrom(interpolated);
        return true;
    }

    /// <summary>Builds the replacement for the single-string-hole shape.</summary>
    /// <param name="expression">The hole's expression.</param>
    /// <returns>The expression itself, parenthesized when it is not a primary expression.</returns>
    private static ExpressionSyntax BuildValueReplacement(ExpressionSyntax expression)
    {
        var stripped = expression.WithoutTrivia();
        return NeedsParentheses(stripped) ? SyntaxFactory.ParenthesizedExpression(stripped) : stripped;
    }

    /// <summary>Builds the plain string literal for the no-holes shape.</summary>
    /// <param name="interpolated">The text-only interpolated string.</param>
    /// <returns>A regular string literal whose value equals the interpolated text, with <c>{{</c>/<c>}}</c> unescaped.</returns>
    private static LiteralExpressionSyntax BuildLiteralReplacement(InterpolatedStringExpressionSyntax interpolated)
        => SyntaxFactory.LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            SyntaxFactory.Literal(GetTextValue(interpolated)));

    /// <summary>Concatenates the unescaped values of a text-only interpolated string's contents.</summary>
    /// <param name="interpolated">The text-only interpolated string.</param>
    /// <returns>The combined text value.</returns>
    private static string GetTextValue(InterpolatedStringExpressionSyntax interpolated)
    {
        var contents = interpolated.Contents;
        if (contents.Count == 0)
        {
            return string.Empty;
        }

        if (contents.Count == 1)
        {
            return UnescapeBraces(((InterpolatedStringTextSyntax)contents[0]).TextToken.ValueText);
        }

        var parts = new string[contents.Count];
        for (var i = 0; i < contents.Count; i++)
        {
            parts[i] = UnescapeBraces(((InterpolatedStringTextSyntax)contents[i]).TextToken.ValueText);
        }

        return string.Concat(parts);
    }

    /// <summary>Collapses the doubled braces an interpolated string uses to escape literal braces.</summary>
    /// <param name="text">The interpolated text segment value.</param>
    /// <returns>The text with <c>{{</c>/<c>}}</c> reduced to <c>{</c>/<c>}</c>.</returns>
    private static string UnescapeBraces(string text)
        => text.Replace("{{", "{").Replace("}}", "}");

    /// <summary>Returns whether a hole expression must be parenthesized once it stands alone.</summary>
    /// <param name="expression">The hole's expression.</param>
    /// <returns><see langword="true"/> for anything that is not a primary expression (conditionals, binaries, assignments, lambdas, and similar).</returns>
    private static bool NeedsParentheses(ExpressionSyntax expression)
        => expression is not (LiteralExpressionSyntax
            or IdentifierNameSyntax
            or MemberAccessExpressionSyntax
            or ConditionalAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or ParenthesizedExpressionSyntax
            or InterpolatedStringExpressionSyntax
            or ObjectCreationExpressionSyntax
            or TupleExpressionSyntax);
}
