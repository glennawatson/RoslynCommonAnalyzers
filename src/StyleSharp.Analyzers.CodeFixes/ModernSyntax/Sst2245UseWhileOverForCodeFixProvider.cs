// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a condition-only <c>for</c> loop as a <c>while</c> loop (SST2245):
/// <c>for (; x &lt; 10; )</c> becomes <c>while (x &lt; 10)</c>. The two empty clauses and their
/// semicolons disappear; the condition, the body, and every piece of surrounding trivia are the
/// original nodes, so nothing else in the loop moves.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2245UseWhileOverForCodeFixProvider))]
[Shared]
public sealed class Sst2245UseWhileOverForCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UseWhileOverFor.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Rewrite the loop as a while loop", nameof(Sst2245UseWhileOverForCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces one reported loop with its <c>while</c> form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="statement">The reported loop.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ForStatementSyntax statement)
        => document.WithSyntaxRoot(root.ReplaceNode(statement, Rewrite(statement)));

    /// <summary>Resolves the reported loop and builds its <c>while</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is ForStatementSyntax statement)
            {
                return Sst2245UseWhileOverForAnalyzer.IsConditionOnlyLoop(statement)
                    ? new NodeReplacement(statement, Rewrite(statement), RewriteCurrent)
                    : null;
            }
        }

        return null;
    }

    /// <summary>Rewrites the current loop during batch FixAll composition.</summary>
    /// <param name="current">The current loop node, possibly carrying nested edits.</param>
    /// <returns>The rewritten loop, or the node unchanged when the shape no longer matches.</returns>
    private static SyntaxNode RewriteCurrent(SyntaxNode current)
        => current is ForStatementSyntax statement && Sst2245UseWhileOverForAnalyzer.IsConditionOnlyLoop(statement)
            ? Rewrite(statement)
            : current;

    /// <summary>Builds the <c>while</c> loop replacing a condition-only <c>for</c> loop.</summary>
    /// <param name="statement">The reported loop; callers must have validated the shape.</param>
    /// <returns>The rewritten loop.</returns>
    private static WhileStatementSyntax Rewrite(ForStatementSyntax statement)
        => SyntaxFactory.WhileStatement(
            statement.AttributeLists,
            SyntaxFactory.Token(statement.ForKeyword.LeadingTrivia, SyntaxKind.WhileKeyword, statement.ForKeyword.TrailingTrivia),
            statement.OpenParenToken,
            statement.Condition!,
            statement.CloseParenToken,
            statement.Statement);
}
