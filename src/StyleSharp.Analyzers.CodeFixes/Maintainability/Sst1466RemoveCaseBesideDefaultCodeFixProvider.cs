// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a case label that shares a switch section with the default label (SST1466). The default
/// label and any other labels in the section are left intact, and leading comments on the removed
/// label are preserved so the fix never eats an explanation of the section.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1466RemoveCaseBesideDefaultCodeFixProvider))]
[Shared]
public sealed class Sst1466RemoveCaseBesideDefaultCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.RemoveCaseBesideDefault.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryGetLabel(root, diagnostic, out var label))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove redundant case label",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, label!)),
                    equivalenceKey: nameof(Sst1466RemoveCaseBesideDefaultCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetLabel(editor.OriginalRoot, diagnostic, out var label))
        {
            return;
        }

        editor.RemoveNode(label!, RemoveOptionsFor(label!));
    }

    /// <summary>Removes the reported case label from its switch section.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="label">The reported case label.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SwitchLabelSyntax label)
        => document.WithSyntaxRoot(root.RemoveNode(label, RemoveOptionsFor(label)) ?? root);

    /// <summary>Resolves the diagnostic's span to its switch label.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="label">The reported label when found.</param>
    /// <returns><see langword="true"/> when a non-default label was found.</returns>
    private static bool TryGetLabel(SyntaxNode root, Diagnostic diagnostic, out SwitchLabelSyntax? label)
    {
        label = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<SwitchLabelSyntax>();
        return label?.IsKind(SyntaxKind.DefaultSwitchLabel) == false;
    }

    /// <summary>Chooses removal options that keep comment banners and preprocessor structure intact.</summary>
    /// <param name="label">The label being removed.</param>
    /// <returns>The removal options.</returns>
    private static SyntaxRemoveOptions RemoveOptionsFor(SwitchLabelSyntax label)
        => HasSignificantLeadingTrivia(label)
            ? SyntaxRemoveOptions.KeepLeadingTrivia | SyntaxRemoveOptions.KeepUnbalancedDirectives
            : SyntaxRemoveOptions.KeepUnbalancedDirectives;

    /// <summary>Returns whether the label's leading trivia carries content worth keeping.</summary>
    /// <param name="label">The label being removed.</param>
    /// <returns><see langword="true"/> when comments or preprocessor directives lead the label.</returns>
    private static bool HasSignificantLeadingTrivia(SwitchLabelSyntax label)
    {
        foreach (var trivia in label.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }
        }

        return false;
    }
}
