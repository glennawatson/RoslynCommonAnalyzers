// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Remove the stray semicolon", nameof(Sst2259RemoveStrayEmptyStatementCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

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
        return type.WithCloseBraceToken(newCloseBrace).WithSemicolonToken(default);
    }
}
