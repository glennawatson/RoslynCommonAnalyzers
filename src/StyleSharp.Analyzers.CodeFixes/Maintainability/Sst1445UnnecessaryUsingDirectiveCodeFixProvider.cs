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
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryGetDirective(root, diagnostic, out var directive))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove unnecessary using directive",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, directive!)),
                    equivalenceKey: nameof(Sst1445UnnecessaryUsingDirectiveCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetDirective(editor.OriginalRoot, diagnostic, out var directive))
        {
            return;
        }

        editor.RemoveNode(directive!, RemoveOptionsFor(directive!));
    }

    /// <summary>Removes the reported directive from the document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="directive">The reported directive.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, UsingDirectiveSyntax directive)
        => document.WithSyntaxRoot(root.RemoveNode(directive, RemoveOptionsFor(directive)) ?? root);

    /// <summary>Resolves the diagnostic's span to its using directive.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="directive">The reported directive when found.</param>
    /// <returns><see langword="true"/> when the directive was found.</returns>
    private static bool TryGetDirective(SyntaxNode root, Diagnostic diagnostic, out UsingDirectiveSyntax? directive)
    {
        directive = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<UsingDirectiveSyntax>();
        return directive is not null;
    }

    /// <summary>Chooses removal options that keep comment banners and preprocessor structure intact.</summary>
    /// <param name="directive">The directive being removed.</param>
    /// <returns>The removal options.</returns>
    private static SyntaxRemoveOptions RemoveOptionsFor(UsingDirectiveSyntax directive)
        => HasSignificantLeadingTrivia(directive)
            ? SyntaxRemoveOptions.KeepLeadingTrivia | SyntaxRemoveOptions.KeepUnbalancedDirectives
            : SyntaxRemoveOptions.KeepUnbalancedDirectives;

    /// <summary>Returns whether the directive's leading trivia carries content worth keeping.</summary>
    /// <param name="directive">The directive being removed.</param>
    /// <returns><see langword="true"/> when comments or preprocessor directives lead the using.</returns>
    private static bool HasSignificantLeadingTrivia(UsingDirectiveSyntax directive)
    {
        foreach (var trivia in directive.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }
        }

        return false;
    }
}
