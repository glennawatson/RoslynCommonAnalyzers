// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a stray trailing semicolon from a declaration that already ends in a brace body (SST2259). The
/// semicolon's own trailing trivia moves onto the closing brace so the newline after the declaration survives.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2259RemoveStrayEmptyStatementCodeFixProvider))]
[Shared]
public sealed class Sst2259RemoveStrayEmptyStatementCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.RemoveStrayEmptyStatement.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Remove the stray semicolon", nameof(Sst2259RemoveStrayEmptyStatementCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported semicolon and rebuilds its declaration without it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        if (!token.IsKind(SyntaxKind.SemicolonToken))
        {
            return null;
        }

        return token.Parent is BaseTypeDeclarationSyntax type && Sst2259RemoveStrayEmptyStatementAnalyzer.HasStraySemicolon(type)
            ? new NodeReplacement(type, RemoveTypeSemicolon(type))
            : null;
    }

    /// <summary>Removes the stray semicolon from a type declaration.</summary>
    /// <param name="type">The type declaration.</param>
    /// <returns>The declaration without its trailing semicolon.</returns>
    private static BaseTypeDeclarationSyntax RemoveTypeSemicolon(BaseTypeDeclarationSyntax type)
    {
        var closeBrace = type.CloseBraceToken;
        var newCloseBrace = closeBrace.WithTrailingTrivia(closeBrace.TrailingTrivia.AddRange(type.SemicolonToken.TrailingTrivia));
        return type switch
        {
            ClassDeclarationSyntax declaration => declaration.Update(
                declaration.AttributeLists,
                declaration.Modifiers,
                declaration.Keyword,
                declaration.Identifier,
                declaration.TypeParameterList,
                declaration.ParameterList,
                declaration.BaseList,
                declaration.ConstraintClauses,
                declaration.OpenBraceToken,
                declaration.Members,
                newCloseBrace,
                default),
            StructDeclarationSyntax declaration => declaration.Update(
                declaration.AttributeLists,
                declaration.Modifiers,
                declaration.Keyword,
                declaration.Identifier,
                declaration.TypeParameterList,
                declaration.ParameterList,
                declaration.BaseList,
                declaration.ConstraintClauses,
                declaration.OpenBraceToken,
                declaration.Members,
                newCloseBrace,
                default),
            InterfaceDeclarationSyntax declaration => declaration.Update(
                declaration.AttributeLists,
                declaration.Modifiers,
                declaration.Keyword,
                declaration.Identifier,
                declaration.TypeParameterList,
                declaration.ParameterList,
                declaration.BaseList,
                declaration.ConstraintClauses,
                declaration.OpenBraceToken,
                declaration.Members,
                newCloseBrace,
                default),
            RecordDeclarationSyntax declaration => RemoveRecordSemicolon(declaration, newCloseBrace),
            EnumDeclarationSyntax declaration => declaration.Update(
                declaration.AttributeLists,
                declaration.Modifiers,
                declaration.EnumKeyword,
                declaration.Identifier,
                declaration.BaseList,
                declaration.OpenBraceToken,
                declaration.Members,
                newCloseBrace,
                default),
            _ => type,
        };
    }

    /// <summary>Removes a record's semicolon while retaining its declaration children.</summary>
    /// <param name="declaration">The record declaration.</param>
    /// <param name="closeBrace">The closing brace carrying the semicolon's trailing trivia.</param>
    /// <returns>The record without its trailing semicolon.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static RecordDeclarationSyntax RemoveRecordSemicolon(RecordDeclarationSyntax declaration, SyntaxToken closeBrace) =>
        declaration.Update(
            declaration.AttributeLists,
            declaration.Modifiers,
            declaration.Keyword,
            declaration.ClassOrStructKeyword,
            declaration.Identifier,
            declaration.TypeParameterList,
            declaration.ParameterList,
            declaration.BaseList,
            declaration.ConstraintClauses,
            declaration.OpenBraceToken,
            declaration.Members,
            closeBrace,
            default);
}
