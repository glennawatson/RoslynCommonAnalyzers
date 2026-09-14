// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers;

/// <summary>Adds the <c>readonly</c> modifier to a record struct that omits one (SST1803).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RecordReadonlyCodeFixProvider))]
[Shared]
public sealed class RecordReadonlyCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(DiagnosticEnclosingNode.Find<RecordDeclarationSyntax>, AddReadonly);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(RecordRules.ReadonlyRecordStruct.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Add 'readonly' modifier",
            nameof(RecordReadonlyCodeFixProvider),
            DiagnosticEnclosingNode.Find<RecordDeclarationSyntax>,
            AddReadonlyAsync);

    /// <summary>Adds the readonly modifier to the record struct, keeping canonical modifier order.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="record">The record struct declaration.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> AddReadonlyAsync(Document document, RecordDeclarationSyntax record, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var updated = AddReadonly(record, SyntaxGenerator.GetGenerator(document));
        return document.WithSyntaxRoot(root!.ReplaceNode(record, updated));
    }

    /// <summary>Adds the readonly modifier to a record struct declaration, keeping canonical modifier order.</summary>
    /// <param name="declaration">The record struct declaration, including any nested batch edits.</param>
    /// <param name="generator">The syntax generator for the document's language.</param>
    /// <returns>The declaration with <c>readonly</c> added.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SyntaxNode AddReadonly(SyntaxNode declaration, SyntaxGenerator generator) =>
        generator.WithModifiers(declaration, generator.GetModifiers(declaration).WithIsReadOnly(true));
}
