// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Deletes an <c>override</c> that only forwards to its base member (SST1181).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantOverrideCodeFixProvider))]
[Shared]
public sealed class RedundantOverrideCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantOverride.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } member)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the redundant override",
                    _ => Task.FromResult(Apply(context.Document, root, member)),
                    equivalenceKey: nameof(RedundantOverrideCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } member)
        {
            return;
        }

        editor.RemoveNode(member, SyntaxRemoveOptions.KeepNoTrivia);
    }

    /// <summary>Removes the redundant override member so the inherited member is used directly.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="member">The redundant override member.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, MemberDeclarationSyntax member)
    {
        var updated = root.RemoveNode(member, SyntaxRemoveOptions.KeepNoTrivia);
        return document.WithSyntaxRoot(updated!);
    }
}
