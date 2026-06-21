// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Applies small flow-shape syntax upgrades for SST2207 and SST2208.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ModernSyntaxFlowCodeFixProvider))]
[Shared]
public sealed class ModernSyntaxFlowCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernSyntaxRules.UseThrowExpression.Id,
        ModernSyntaxRules.InlineOutVariableDeclaration.Id);

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
            var title = diagnostic.Id switch
            {
                "SST2207" => "Use a throw expression",
                "SST2208" => "Inline the out declaration",
                _ => null
            };

            if (title is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => ApplyAsync(context.Document, root, diagnostic, cancellationToken),
                    equivalenceKey: diagnostic.Id),
                diagnostic);
        }
    }

    /// <summary>Applies one flow syntax fix.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> ApplyAsync(
        Document document,
        SyntaxNode root,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return document;
        }

        return diagnostic.Id switch
        {
            "SST2207" => ApplyThrowExpression(document, root, diagnostic, model, cancellationToken),
            "SST2208" => ApplyInlineOutDeclaration(document, root, diagnostic, model, cancellationToken),
            _ => document
        };
    }

    /// <summary>Replaces a null guard plus return with a return containing a throw expression.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static Document ApplyThrowExpression(
        Document document,
        SyntaxNode root,
        Diagnostic diagnostic,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var ifStatement = FindAncestor<IfStatementSyntax>(root, diagnostic.Location.SourceSpan);
        if (ifStatement?.Parent is not BlockSyntax block
            || !ModernSyntaxFlowAnalyzer.TryGetThrowExpressionCandidate(ifStatement, model, cancellationToken, out var throwValue)
            || !ModernSyntaxFlowAnalyzer.TryGetNextStatement(ifStatement, out var nextStatement)
            || nextStatement is not ReturnStatementSyntax { Expression: { } returnedValue })
        {
            return document;
        }

        var coalesce = SyntaxFactory.BinaryExpression(
            SyntaxKind.CoalesceExpression,
            returnedValue.WithoutTrivia(),
            SyntaxFactory.ThrowExpression(throwValue.WithoutTrivia()));
        var replacement = SyntaxFactory.ReturnStatement(coalesce).WithTriviaFrom(ifStatement);
        var index = block.Statements.IndexOf(ifStatement);
        var statements = block.Statements.Replace(ifStatement, replacement).RemoveAt(index + 1);
        var updatedBlock = block.WithStatements(statements);
        return document.WithSyntaxRoot(root.ReplaceNode(block, updatedBlock));
    }

    /// <summary>Moves an out local declaration into the following out argument.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static Document ApplyInlineOutDeclaration(
        Document document,
        SyntaxNode root,
        Diagnostic diagnostic,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var declaration = FindAncestor<LocalDeclarationStatementSyntax>(root, diagnostic.Location.SourceSpan);
        if (declaration?.Parent is not BlockSyntax block
            || !ModernSyntaxFlowAnalyzer.TryGetNextStatement(declaration, out var nextStatement)
            || !ModernSyntaxFlowAnalyzer.TryGetInlineOutArgument(declaration, nextStatement, model, cancellationToken, out var argument)
            || argument.Expression is not IdentifierNameSyntax identifier)
        {
            return document;
        }

        var declarationExpression = SyntaxFactory.DeclarationExpression(
            SyntaxFactory.IdentifierName("var"),
            SyntaxFactory.SingleVariableDesignation(identifier.Identifier.WithoutTrivia()));
        var replacementArgument = argument.WithExpression(declarationExpression).WithTriviaFrom(argument);
        var updatedNext = nextStatement.ReplaceNode(argument, replacementArgument);
        var index = block.Statements.IndexOf(declaration);
        var statements = block.Statements.Replace(nextStatement, (StatementSyntax)updatedNext).RemoveAt(index);
        var updatedBlock = block.WithStatements(statements);
        return document.WithSyntaxRoot(root.ReplaceNode(block, updatedBlock));
    }

    /// <summary>Finds the node at a span or one of its ancestors.</summary>
    /// <typeparam name="T">The ancestor node type to find.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <returns>The matching node, or <see langword="null"/>.</returns>
    private static T? FindAncestor<T>(SyntaxNode root, TextSpan span)
        where T : SyntaxNode
    {
        var node = root.FindToken(span.Start).Parent;
        while (node is not null)
        {
            if (node is T matched)
            {
                return matched;
            }

            node = node.Parent;
        }

        return null;
    }
}
