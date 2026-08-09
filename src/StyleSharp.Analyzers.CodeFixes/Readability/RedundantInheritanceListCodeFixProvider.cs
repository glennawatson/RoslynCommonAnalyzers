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
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Remove the redundant base type", nameof(RedundantInheritanceListCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported base type and builds the declaration without it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BaseTypeSyntax>() is not { Parent: BaseListSyntax baseList })
        {
            return null;
        }

        var typeDeclaration = baseList.Parent!;

        return new NodeReplacement(typeDeclaration, Rewrite(typeDeclaration, baseList));
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
