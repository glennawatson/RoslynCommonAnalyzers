// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Gives every loop iteration a fresh <c>ValueTask</c> (PSH1316) by moving the producing declaration
/// from outside the loop to the top of its body. Offered only for the loop form, and only when the
/// local is used nowhere but inside the loop.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1316ConsumeValueTaskOnceCodeFixProvider))]
[Shared]
public sealed class Psh1316ConsumeValueTaskOnceCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.ConsumeValueTaskOnce.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryResolve(root, model, diagnostic, out var declaration, out var body))
            {
                continue;
            }

            var captured = declaration!;
            var capturedBody = body!;
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Move the producing call into the loop",
                    _ => Task.FromResult(Move(context.Document, root, captured, capturedBody)),
                    equivalenceKey: nameof(Psh1316ConsumeValueTaskOnceCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Resolves the consumed local's declaration and the loop body it should move into.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="declaration">The producing declaration to move.</param>
    /// <param name="body">The loop body to move it into.</param>
    /// <returns><see langword="true"/> when a safe move is available.</returns>
    private static bool TryResolve(SyntaxNode root, SemanticModel model, Diagnostic diagnostic, out LocalDeclarationStatementSyntax? declaration, out BlockSyntax? body)
    {
        declaration = null;
        body = null;
        if (root.FindNode(diagnostic.Location.SourceSpan) is not IdentifierNameSyntax identifier
            || model.GetSymbolInfo(identifier).Symbol is not ILocalSymbol local
            || local.DeclaringSyntaxReferences is not [var reference]
            || reference.GetSyntax() is not VariableDeclaratorSyntax { Initializer: not null, Parent.Parent: LocalDeclarationStatementSyntax statement }
            || statement.Declaration.Variables.Count != 1
            || !statement.UsingKeyword.IsKind(SyntaxKind.None)
            || NearestLoopBody(identifier) is not { } loopBody
            || UsedOutside(loopBody, local, model, identifier))
        {
            return false;
        }

        declaration = statement;
        body = loopBody;
        return true;
    }

    /// <summary>Gets the block body of the loop nearest an identifier.</summary>
    /// <param name="node">The consumed identifier.</param>
    /// <returns>The loop body block, or <see langword="null"/>.</returns>
    private static BlockSyntax? NearestLoopBody(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (LoopBody(current) is { } body)
            {
                return body as BlockSyntax;
            }

            if (current is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or MemberDeclarationSyntax)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Gets a loop's body statement, or nothing when the node is not a loop.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The body statement, or <see langword="null"/>.</returns>
    private static StatementSyntax? LoopBody(SyntaxNode node) => node switch
    {
        ForStatementSyntax statement => statement.Statement,
        ForEachStatementSyntax statement => statement.Statement,
        WhileStatementSyntax statement => statement.Statement,
        DoStatementSyntax statement => statement.Statement,
        _ => null,
    };

    /// <summary>Returns whether the local is referenced anywhere outside the loop body.</summary>
    /// <param name="body">The loop body.</param>
    /// <param name="local">The local.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="ignore">The reported identifier, always inside the loop.</param>
    /// <returns><see langword="true"/> when moving the declaration would strand a use.</returns>
    private static bool UsedOutside(BlockSyntax body, ILocalSymbol local, SemanticModel model, IdentifierNameSyntax ignore)
    {
        foreach (var reference in local.DeclaringSyntaxReferences)
        {
            var scope = reference.GetSyntax().FirstAncestorOrSelf<BlockSyntax>();
            if (scope is null)
            {
                return true;
            }

            foreach (var node in scope.DescendantNodes())
            {
                if (node is IdentifierNameSyntax identifier
                    && identifier != ignore
                    && identifier.Identifier.ValueText == local.Name
                    && !body.Span.Contains(identifier.Span)
                    && !IsDeclarator(identifier)
                    && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier).Symbol, local))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether an identifier is the local's own declarator name.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns><see langword="true"/> when it is a declarator rather than a use.</returns>
    private static bool IsDeclarator(IdentifierNameSyntax identifier) => identifier.Parent is VariableDeclaratorSyntax;

    /// <summary>Moves the producing declaration to the top of the loop body.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="declaration">The producing declaration.</param>
    /// <param name="body">The loop body.</param>
    /// <returns>The updated document.</returns>
    private static Document Move(Document document, SyntaxNode root, LocalDeclarationStatementSyntax declaration, BlockSyntax body)
    {
        var indent = body.Statements.Count > 0 ? body.Statements[0].GetLeadingTrivia() : declaration.GetLeadingTrivia();
        var moved = declaration
            .WithLeadingTrivia(indent)
            .WithTrailingTrivia(declaration.GetTrailingTrivia());

        var trackedRoot = root.TrackNodes(declaration, body);
        var currentBody = trackedRoot.GetCurrentNode(body)!;
        var withInsert = trackedRoot.ReplaceNode(currentBody, currentBody.WithStatements(currentBody.Statements.Insert(0, moved)));

        var currentDeclaration = withInsert.GetCurrentNode(declaration)!;
        var finalRoot = withInsert.RemoveNode(currentDeclaration, SyntaxRemoveOptions.KeepNoTrivia)!;
        return document.WithSyntaxRoot(finalRoot);
    }
}
