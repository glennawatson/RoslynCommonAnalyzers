// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Replaces a null a <c>ToString</c> override could return with <c>string.Empty</c> (SST2431).
/// </summary>
/// <remarks>
/// The whole reported node is swapped — <c>null</c>, <c>null!</c>, a parenthesised or cast null, or the null
/// branch of a conditional — and it keeps its surrounding trivia, so the fix is the smallest edit that turns a
/// null return into an empty string without touching the rest of the expression.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2431ToStringReturnsNullCodeFixProvider))]
[Shared]
public sealed class Sst2431ToStringReturnsNullCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.ToStringReturnsNull.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Return string.Empty instead of null",
            nameof(Sst2431ToStringReturnsNullCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported null and replaces it with <c>string.Empty</c>.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not ExpressionSyntax original)
        {
            return null;
        }

        var replacement = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
            SyntaxFactory.IdentifierName("Empty"));

        return new NodeReplacement(original, replacement.WithTriviaFrom(original));
    }
}
