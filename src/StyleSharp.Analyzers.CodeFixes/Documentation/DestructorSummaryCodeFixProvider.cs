// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Replaces a destructor summary with the standard "Finalizes an instance…" text (SST1643).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DestructorSummaryCodeFixProvider))]
[Shared]
public sealed class DestructorSummaryCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.DestructorStandardText.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TextChangeCodeFix.RegisterAsync(
            context,
            static (root, diagnostic) => DocumentationElementFix.TryFindMemberSummary<DestructorDeclarationSyntax>(root, diagnostic, out _, out _) ? "Use the standard destructor summary" : null,
            nameof(DestructorSummaryCodeFixProvider),
            RegisterTextChanges);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes) =>
        DocumentationElementFix.AppendStandardSummary<DestructorDeclarationSyntax>(root, diagnostic, changes, DocumentationConventions.DestructorStandardSummary);
}
