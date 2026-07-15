// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Moves a <c>switch</c> statement's <c>default</c> section to the end (SST1219). The move is always
/// behaviour-preserving: sections do not fall through, and jumps target labels rather than positions.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1219DefaultSectionLastCodeFixProvider))]
[Shared]
public sealed class Sst1219DefaultSectionLastCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(OrderingRules.DefaultSectionLast.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Move the default section to the end",
            nameof(Sst1219DefaultSectionLastCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported switch and moves its default section last.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<SwitchSectionSyntax>() is not { } section
            || section.Parent is not SwitchStatementSyntax switchStatement)
        {
            return null;
        }

        var index = switchStatement.Sections.IndexOf(section);
        if (index < 0 || index == switchStatement.Sections.Count - 1)
        {
            return null;
        }

        return new NodeReplacement(switchStatement, Move(switchStatement, index), current => Rewrite(current, index));
    }

    /// <summary>Re-applies the move after any nested batch edit.</summary>
    /// <param name="current">The current switch statement.</param>
    /// <param name="index">The default section's position.</param>
    /// <returns>The reordered switch, or the node unchanged.</returns>
    private static SyntaxNode Rewrite(SyntaxNode current, int index)
        => current is SwitchStatementSyntax switchStatement && index >= 0 && index < switchStatement.Sections.Count - 1
            ? Move(switchStatement, index)
            : current;

    /// <summary>Moves the section at a position to the end of the switch.</summary>
    /// <param name="switchStatement">The switch statement.</param>
    /// <param name="index">The section's position.</param>
    /// <returns>The reordered switch.</returns>
    private static SwitchStatementSyntax Move(SwitchStatementSyntax switchStatement, int index)
    {
        var section = switchStatement.Sections[index];
        var reordered = switchStatement.Sections.RemoveAt(index).Add(section);
        return switchStatement.WithSections(reordered);
    }
}
