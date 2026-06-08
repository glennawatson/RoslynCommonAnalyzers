// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers;

/// <summary>Changes a misleading <c>public</c> member of a non-public type to <c>internal</c> (SST1416).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NoPublicOnInternalTypeCodeFixProvider))]
[Shared]
public sealed class NoPublicOnInternalTypeCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.NoPublicOnInternalType.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
                    "Change 'public' to 'internal'",
                    cancellationToken => MakeInternalAsync(context.Document, member, cancellationToken),
                    equivalenceKey: nameof(NoPublicOnInternalTypeCodeFixProvider)),
                diagnostic);
        }
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
