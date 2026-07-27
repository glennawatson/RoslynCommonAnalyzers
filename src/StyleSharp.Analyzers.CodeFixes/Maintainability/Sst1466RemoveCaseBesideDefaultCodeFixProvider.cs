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
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => RemoveNodeCodeFix.RegisterAsync(context, "Remove redundant case label", nameof(Sst1466RemoveCaseBesideDefaultCodeFixProvider), TrySelect);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => RemoveNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TrySelect);

    /// <summary>Resolves the diagnostic's span to the non-default case label it reports.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to remove, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeRemoval? TrySelect(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<SwitchLabelSyntax>() is not { } label
            || label.IsKind(SyntaxKind.DefaultSwitchLabel))
        {
            return null;
        }

        return NodeRemoval.PreservingLeadingContent(label);
    }
}
