// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant base type from an inheritance list (SST1177).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantInheritanceListCodeFixProvider))]
[Shared]
public sealed class RedundantInheritanceListCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantInheritanceList.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BaseTypeSyntax>() is not { Parent: BaseListSyntax } baseType)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the redundant base type",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, baseType)),
                    equivalenceKey: nameof(RedundantInheritanceListCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Removes the base type, dropping the whole base list when it was the only entry.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="baseType">The redundant base type to remove.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BaseTypeSyntax baseType)
    {
        var baseList = (BaseListSyntax)baseType.Parent!;
        var typeDeclaration = baseList.Parent!;

        if (baseList.Types.Count > 1)
        {
            var trimmedList = baseList.WithTypes(baseList.Types.RemoveAt(0));
            return document.WithSyntaxRoot(root.ReplaceNode(typeDeclaration, typeDeclaration.ReplaceNode(baseList, trimmedList)));
        }

        // Removing the whole base list also drops the newline that lived as the base type's trailing
        // trivia, so move that trivia onto the brace and clear the identifier's trailing space.
        var listTrivia = baseList.GetTrailingTrivia();
        var stripped = (BaseTypeDeclarationSyntax)typeDeclaration.RemoveNode(baseList, SyntaxRemoveOptions.KeepNoTrivia)!;
        var brace = stripped.OpenBraceToken;
        var precedingToken = brace.GetPreviousToken();
        var updated = stripped.ReplaceTokens(
            [precedingToken, brace],
            (original, _) => original == brace
                ? brace.WithLeadingTrivia(listTrivia.AddRange(brace.LeadingTrivia))
                : precedingToken.WithTrailingTrivia());

        return document.WithSyntaxRoot(root.ReplaceNode(typeDeclaration, updated));
    }
}
