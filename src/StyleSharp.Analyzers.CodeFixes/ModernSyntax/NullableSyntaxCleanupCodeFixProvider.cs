// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes nullable syntax that has no local effect.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullableSyntaxCleanupCodeFixProvider))]
[Shared]
public sealed class NullableSyntaxCleanupCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernSyntaxRules.RemoveUnneededNullForgiving.Id,
        ModernSyntaxRules.RemoveRepeatedNullableDirective.Id,
        ModernSyntaxRules.RemoveUnusedNullableRestore.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            static diagnostic => diagnostic.Id == ModernSyntaxRules.RemoveUnneededNullForgiving.Id
                ? "Remove null-forgiving operator"
                : "Remove nullable directive",
            static diagnostic => diagnostic.Id,
            static (_, _) => true,
            ApplyAsync);

    /// <summary>Applies one nullable syntax cleanup.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> ApplyAsync(
        Document document,
        SyntaxNode root,
        Diagnostic diagnostic,
        CancellationToken cancellationToken) =>
        diagnostic.Id switch
        {
            "SST2209" => ApplyNullForgiving(document, root, diagnostic),
            "SST2210" or "SST2211" => await RemoveDirectiveAsync(document, diagnostic, cancellationToken).ConfigureAwait(false),
            _ => document
        };

    /// <summary>Removes a null-forgiving postfix operator.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document.</returns>
    private static Document ApplyNullForgiving(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        var suppression = DiagnosticAncestor.Find<PostfixUnaryExpressionSyntax>(root, diagnostic.Location.SourceSpan);
        return suppression is null ? document : document.WithSyntaxRoot(root.ReplaceNode(suppression, suppression.Operand.WithTriviaFrom(suppression)));
    }

    /// <summary>Removes the whole line containing a nullable directive.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> RemoveDirectiveAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var line = text.Lines.GetLineFromPosition(diagnostic.Location.SourceSpan.Start);
        return document.WithText(text.WithChanges(new TextChange(line.SpanIncludingLineBreak, string.Empty)));
    }
}
