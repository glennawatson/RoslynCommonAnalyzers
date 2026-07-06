// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Inserts the <c>await</c> keyword on a reported synchronous <c>using</c> statement or using
/// declaration (PSH1310), moving the statement's leading trivia onto the inserted keyword so
/// indentation and comments stay where the author put them.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1310UseAwaitUsingCodeFixProvider))]
[Shared]
public sealed class Psh1310UseAwaitUsingCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.UseAwaitUsing.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use await using", nameof(Psh1310UseAwaitUsingCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Inserts the await keyword on the reported using statement or declaration.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="statement">The reported using statement or using declaration.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, StatementSyntax statement)
        => document.WithSyntaxRoot(root.ReplaceNode(statement, Rewrite(statement)));

    /// <summary>Resolves the reported statement and builds its awaited replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => TryGetStatement(root, diagnostic) is { } statement
            ? new NodeReplacement(statement, Rewrite(statement), static current => Rewrite((StatementSyntax)current))
            : null;

    /// <summary>Finds the reported synchronous using statement or using declaration for a diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The statement to rewrite, or <see langword="null"/> when the shape no longer matches.</returns>
    private static StatementSyntax? TryGetStatement(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) switch
        {
            UsingStatementSyntax usingStatement when usingStatement.AwaitKeyword.IsKind(SyntaxKind.None) => usingStatement,
            LocalDeclarationStatementSyntax declarationStatement when declarationStatement.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)
                && declarationStatement.AwaitKeyword.IsKind(SyntaxKind.None) => declarationStatement,
            _ => null,
        };

    /// <summary>Rewrites either reported statement form to its awaited equivalent.</summary>
    /// <param name="statement">The using statement or using declaration to rewrite.</param>
    /// <returns>The rewritten statement.</returns>
    private static StatementSyntax Rewrite(StatementSyntax statement)
        => statement switch
        {
            UsingStatementSyntax usingStatement => RewriteUsingStatement(usingStatement),
            LocalDeclarationStatementSyntax declarationStatement => RewriteUsingDeclaration(declarationStatement),
            _ => statement,
        };

    /// <summary>Inserts the await keyword on a using statement, keeping the statement's leading trivia on it.</summary>
    /// <param name="usingStatement">The using statement to rewrite.</param>
    /// <returns>The rewritten using statement.</returns>
    private static UsingStatementSyntax RewriteUsingStatement(UsingStatementSyntax usingStatement)
        => usingStatement
            .WithAwaitKeyword(CreateAwaitKeyword(usingStatement.UsingKeyword))
            .WithUsingKeyword(usingStatement.UsingKeyword.WithLeadingTrivia(SyntaxFactory.TriviaList()))
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);

    /// <summary>Inserts the await keyword on a using declaration, keeping the statement's leading trivia on it.</summary>
    /// <param name="declarationStatement">The using declaration to rewrite.</param>
    /// <returns>The rewritten using declaration.</returns>
    private static LocalDeclarationStatementSyntax RewriteUsingDeclaration(LocalDeclarationStatementSyntax declarationStatement)
        => declarationStatement
            .WithAwaitKeyword(CreateAwaitKeyword(declarationStatement.UsingKeyword))
            .WithUsingKeyword(declarationStatement.UsingKeyword.WithLeadingTrivia(SyntaxFactory.TriviaList()))
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);

    /// <summary>Builds the await keyword carrying the original using keyword's leading trivia.</summary>
    /// <param name="usingKeyword">The original using keyword.</param>
    /// <returns>The await keyword to insert.</returns>
    private static SyntaxToken CreateAwaitKeyword(SyntaxToken usingKeyword)
        => SyntaxFactory.Token(SyntaxKind.AwaitKeyword)
            .WithLeadingTrivia(usingKeyword.LeadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.Space);
}
