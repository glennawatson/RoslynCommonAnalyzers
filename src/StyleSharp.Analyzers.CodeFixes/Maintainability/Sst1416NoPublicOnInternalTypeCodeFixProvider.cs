// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers;

/// <summary>Changes a misleading <c>public</c> member of a non-public type to <c>internal</c> (SST1416).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1416NoPublicOnInternalTypeCodeFixProvider))]
[Shared]
public sealed class Sst1416NoPublicOnInternalTypeCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RegisterBatchEdits);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.NoPublicOnInternalType.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Change 'public' to 'internal'",
            nameof(Sst1416NoPublicOnInternalTypeCodeFixProvider),
            DiagnosticEnclosingNode.Find<MemberDeclarationSyntax>,
            MakeInternalAsync);

    /// <summary>Registers the edits that fix one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal static void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (DiagnosticEnclosingNode.Find<MemberDeclarationSyntax>(editor.OriginalRoot, diagnostic) is not { } member)
        {
            return;
        }

        editor.ReplaceNode(member, editor.Generator.WithAccessibility(member, Accessibility.Internal));
    }

    /// <summary>Sets the member's accessibility to internal.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="member">The public member to demote.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> MakeInternalAsync(Document document, MemberDeclarationSyntax member, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var generator = SyntaxGenerator.GetGenerator(document);
        return document.WithSyntaxRoot(root!.ReplaceNode(member, generator.WithAccessibility(member, Accessibility.Internal)));
    }
}
