// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Adds the <c>readonly</c> modifier to a reported struct declaration (PSH1014). The modifier
/// lands after any access modifiers and before a <c>ref</c> modifier, matching the standard
/// ordering, and the declaration's leading trivia moves onto it when it becomes the first
/// token.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1014ReadonlyStructCodeFixProvider))]
[Shared]
public sealed class Psh1014ReadonlyStructCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.MakeStructReadonly.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Make the struct readonly", nameof(Psh1014ReadonlyStructCodeFixProvider), CanRewrite, TryRewrite);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Builds the declaration with a readonly modifier in standard position.</summary>
    /// <param name="declaration">The struct declaration to rewrite.</param>
    /// <returns>The readonly declaration.</returns>
    internal static TypeDeclarationSyntax AddReadonlyModifier(TypeDeclarationSyntax declaration)
    {
        var modifiers = declaration.Modifiers;
        if (modifiers.Count == 0)
        {
            return AddFirstReadonlyModifier(declaration);
        }

        var readonlyToken = SyntaxFactory.Token(default, SyntaxKind.ReadOnlyKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        var insertIndex = modifiers.Count;
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (!modifiers[i].IsKind(SyntaxKind.RefKeyword) && !modifiers[i].IsKind(SyntaxKind.UnsafeKeyword))
            {
                continue;
            }

            insertIndex = i;
            break;
        }

        if (insertIndex == 0)
        {
            var first = modifiers[0];
            return declaration.WithModifiers(
                modifiers.Replace(first, first.WithLeadingTrivia())
                    .Insert(0, readonlyToken.WithLeadingTrivia(first.LeadingTrivia)));
        }

        return declaration.WithModifiers(modifiers.Insert(insertIndex, readonlyToken));
    }

    /// <summary>Adds the first modifier while transferring the declaration keyword's leading trivia.</summary>
    /// <param name="declaration">The declaration without modifiers.</param>
    /// <returns>The declaration with its readonly modifier.</returns>
    private static TypeDeclarationSyntax AddFirstReadonlyModifier(TypeDeclarationSyntax declaration)
    {
        var keyword = declaration.Keyword;
        var updatedModifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(
            keyword.LeadingTrivia,
            SyntaxKind.ReadOnlyKeyword,
            SyntaxFactory.TriviaList(SyntaxFactory.Space)));
        var updatedKeyword = keyword.WithLeadingTrivia();
        return declaration switch
        {
            StructDeclarationSyntax structure => structure.Update(
                structure.AttributeLists,
                updatedModifiers,
                updatedKeyword,
                structure.Identifier,
                structure.TypeParameterList,
                structure.ParameterList,
                structure.BaseList,
                structure.ConstraintClauses,
                structure.OpenBraceToken,
                structure.Members,
                structure.CloseBraceToken,
                structure.SemicolonToken),
            RecordDeclarationSyntax record => record.Update(
                record.AttributeLists,
                updatedModifiers,
                updatedKeyword,
                record.ClassOrStructKeyword,
                record.Identifier,
                record.TypeParameterList,
                record.ParameterList,
                record.BaseList,
                record.ConstraintClauses,
                record.OpenBraceToken,
                record.Members,
                record.CloseBraceToken,
                record.SemicolonToken),
            _ => declaration.WithModifiers(updatedModifiers).WithKeyword(updatedKeyword)
        };
    }

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan)is TypeDeclarationSyntax { RawKind: (int)SyntaxKind.StructDeclaration or (int)SyntaxKind.RecordStructDeclaration } declaration
            && !declaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);

    /// <summary>Resolves the reported struct declaration and builds it with the modifier added.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is TypeDeclarationSyntax { RawKind: (int)SyntaxKind.StructDeclaration or (int)SyntaxKind.RecordStructDeclaration } declaration
            && !declaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
            ? new NodeReplacement(declaration, AddReadonlyModifier(declaration))
            : null;
}
