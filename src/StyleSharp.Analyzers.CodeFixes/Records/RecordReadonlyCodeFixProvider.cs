// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers;

/// <summary>Adds the <c>readonly</c> modifier to a record struct that omits one (SST1803).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RecordReadonlyCodeFixProvider))]
[Shared]
public sealed class RecordReadonlyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(RecordRules.ReadonlyRecordStruct.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<RecordDeclarationSyntax>() is not { } record)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add 'readonly' modifier",
                    cancellationToken => AddReadonlyAsync(context.Document, record, cancellationToken),
                    equivalenceKey: nameof(RecordReadonlyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<RecordDeclarationSyntax>() is not { } record)
        {
            return;
        }

        var generator = editor.Generator;
        var updated = generator.WithModifiers(record, generator.GetModifiers(record).WithIsReadOnly(true));
        editor.ReplaceNode(record, updated);
    }

    /// <summary>Adds the readonly modifier to the record struct, keeping canonical modifier order.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="record">The record struct declaration.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> AddReadonlyAsync(Document document, RecordDeclarationSyntax record, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var generator = SyntaxGenerator.GetGenerator(document);
        var updated = generator.WithModifiers(record, generator.GetModifiers(record).WithIsReadOnly(true));
        return document.WithSyntaxRoot(root!.ReplaceNode(record, updated));
    }
}
