// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant base type from an inheritance list (SST1177).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantInheritanceListCodeFixProvider))]
[Shared]
public sealed class RedundantInheritanceListCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantInheritanceList.Id);

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

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BaseTypeSyntax>() is not { Parent: BaseListSyntax } baseType)
        {
            return;
        }

        var baseList = (BaseListSyntax)baseType.Parent!;
        var typeDeclaration = baseList.Parent!;
        editor.ReplaceNode(typeDeclaration, Rewrite(typeDeclaration, baseList));
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
        return document.WithSyntaxRoot(root.ReplaceNode(typeDeclaration, Rewrite(typeDeclaration, baseList)));
    }

    /// <summary>Builds the type declaration with the redundant base type removed.</summary>
    /// <param name="typeDeclaration">The type declaration owning the base list.</param>
    /// <param name="baseList">The base list to trim or drop.</param>
    /// <returns>The rewritten type declaration node.</returns>
    private static SyntaxNode Rewrite(SyntaxNode typeDeclaration, BaseListSyntax baseList)
    {
        if (baseList.Types.Count > 1)
        {
            var trimmedList = baseList.WithTypes(baseList.Types.RemoveAt(0));
            return typeDeclaration.ReplaceNode(baseList, trimmedList);
        }

        // Removing the whole base list also drops the newline that lived as the base type's trailing
        // trivia, so move that trivia onto the brace and clear the identifier's trailing space.
        var listTrivia = baseList.GetTrailingTrivia();
        var stripped = (BaseTypeDeclarationSyntax)typeDeclaration.RemoveNode(baseList, SyntaxRemoveOptions.KeepNoTrivia)!;
        var brace = stripped.OpenBraceToken;
        var precedingToken = brace.GetPreviousToken();
        return stripped.ReplaceTokens(
            [precedingToken, brace],
            (original, _) => original == brace
                ? brace.WithLeadingTrivia(listTrivia.AddRange(brace.LeadingTrivia))
                : precedingToken.WithTrailingTrivia());
    }
}
