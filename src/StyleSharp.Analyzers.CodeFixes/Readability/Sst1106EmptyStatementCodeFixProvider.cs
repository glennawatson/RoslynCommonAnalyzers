// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes an empty statement (SST1106). The fix is only offered when the semicolon stands in a
/// block, switch section, or top-level statement list, where deleting it cannot change control
/// flow — an empty statement embedded as a loop or <c>if</c> body is left for manual review.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1106EmptyStatementCodeFixProvider))]
[Shared]
public sealed class Sst1106EmptyStatementCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TrySelect);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.EmptyStatement.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        RemoveNodeCodeFix.RegisterAsync(context, "Remove empty statement", nameof(Sst1106EmptyStatementCodeFixProvider), TrySelect);

    /// <summary>Resolves the reported empty statement when deleting it cannot change control flow.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to remove, or <see langword="null"/> when the semicolon is an embedded statement.</returns>
    private static NodeRemoval? TrySelect(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not EmptyStatementSyntax { Parent: BlockSyntax or SwitchSectionSyntax or GlobalStatementSyntax } statement)
        {
            return null;
        }

        // A top-level statement is wrapped in a GlobalStatementSyntax; remove the wrapper too.
        return statement.Parent is GlobalStatementSyntax global
            ? new NodeRemoval(global)
            : new NodeRemoval(statement);
    }
}
