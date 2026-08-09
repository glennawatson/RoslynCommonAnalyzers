// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes braces from a switch section that scopes nothing (SST1534), lifting the statements into the
/// section itself. The rewritten section is formatter-annotated so the lifted statements are re-indented.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1534RedundantSwitchSectionBracesCodeFixProvider))]
[Shared]
public sealed class Sst1534RedundantSwitchSectionBracesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(LayoutRules.RedundantSwitchSectionBraces.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Remove the switch section braces",
            nameof(Sst1534RedundantSwitchSectionBracesCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported section and replaces it with one holding the block's statements.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<SwitchSectionSyntax>() is not { } section
            || section.Statements.Count != 1
            || section.Statements[0] is not BlockSyntax block)
        {
            return null;
        }

        var replacement = section
            .WithStatements(block.Statements)
            .WithAdditionalAnnotations(Formatter.Annotation);

        return new NodeReplacement(section, replacement);
    }
}
