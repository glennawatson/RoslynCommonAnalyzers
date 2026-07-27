// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes an unnecessary using directive (SST1445). Leading comment banners and preprocessor
/// directives are preserved so removing the first using never eats a file header, and unbalanced
/// conditional directives inside the removed span are kept.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1445UnnecessaryUsingDirectiveCodeFixProvider))]
[Shared]
public sealed class Sst1445UnnecessaryUsingDirectiveCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.UnnecessaryUsingDirective.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => RemoveNodeCodeFix.RegisterAsync(context, "Remove unnecessary using directive", nameof(Sst1445UnnecessaryUsingDirectiveCodeFixProvider), TrySelect);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => RemoveNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TrySelect);

    /// <summary>Resolves the diagnostic's span to the using directive it reports.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to remove, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeRemoval? TrySelect(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<UsingDirectiveSyntax>() is { } directive
            ? NodeRemoval.PreservingLeadingContent(directive)
            : null;
}
