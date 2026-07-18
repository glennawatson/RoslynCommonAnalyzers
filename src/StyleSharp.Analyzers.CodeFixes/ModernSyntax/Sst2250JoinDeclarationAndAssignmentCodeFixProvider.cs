// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Joins a bare local declaration with the immediately following first assignment (SST2250):
/// <c>int x;</c> plus <c>x = 5;</c> becomes <c>int x = 5;</c>. The declaration keeps its position,
/// type, modifiers, and trivia; the assignment's value moves onto it and the separate assignment
/// statement is removed. Because the assignment already compiled against the declared type, the joined
/// initializer binds the same way, so the merged declaration compiles whenever the source did.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2250JoinDeclarationAndAssignmentCodeFixProvider))]
[Shared]
public sealed class Sst2250JoinDeclarationAndAssignmentCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.JoinDeclarationAndAssignment.Id);

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
            if (Resolve(root, diagnostic) is not { } edit)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Join the declaration with its assignment",
                    _ => Task.FromResult(Apply(context.Document, root, edit)),
                    equivalenceKey: nameof(Sst2250JoinDeclarationAndAssignmentCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, diagnostic) is not { } edit)
        {
            return;
        }

        editor.ReplaceNode(edit.Local, edit.Merged);
        editor.RemoveNode(edit.Assignment);
    }

    /// <summary>Applies one join by replacing the declaration and dropping the assignment statement.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="edit">The resolved edit.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, JoinEdit edit)
    {
        var block = (BlockSyntax)edit.Local.Parent!;
        var statements = block.Statements.Replace(edit.Local, edit.Merged);
        statements = statements.RemoveAt(block.Statements.IndexOf(edit.Assignment));
        return document.WithSyntaxRoot(root.ReplaceNode(block, block.WithStatements(statements)));
    }

    /// <summary>Resolves the reported declaration into the nodes the fix swaps.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The edit, or <see langword="null"/> when the shape no longer matches.</returns>
    private static JoinEdit? Resolve(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<LocalDeclarationStatementSyntax>() is not { } local
            || !Sst2250JoinDeclarationAndAssignmentAnalyzer.TryGetJoinCandidate(local, out var variable, out var assignment))
        {
            return null;
        }

        return new JoinEdit(local, assignment, BuildMerged(local, variable, assignment));
    }

    /// <summary>Builds the merged declaration carrying the assignment's value as its initializer.</summary>
    /// <param name="local">The bare local declaration.</param>
    /// <param name="variable">The single declarator.</param>
    /// <param name="assignment">The following assignment statement.</param>
    /// <returns>The declaration with an initializer.</returns>
    private static LocalDeclarationStatementSyntax BuildMerged(
        LocalDeclarationStatementSyntax local,
        VariableDeclaratorSyntax variable,
        ExpressionStatementSyntax assignment)
    {
        var value = ((AssignmentExpressionSyntax)assignment.Expression).Right;
        var equalsToken = SyntaxFactory.Token(SyntaxKind.EqualsToken)
            .WithLeadingTrivia(SyntaxFactory.Space)
            .WithTrailingTrivia(SyntaxFactory.Space);
        var initialized = variable.WithInitializer(SyntaxFactory.EqualsValueClause(equalsToken, value.WithoutTrivia()));
        return local.ReplaceNode(variable, initialized);
    }

    /// <summary>The declaration, the assignment to drop, and the merged declaration replacing the former.</summary>
    /// <param name="Local">The bare local declaration being replaced.</param>
    /// <param name="Assignment">The following assignment statement being removed.</param>
    /// <param name="Merged">The declaration carrying the joined initializer.</param>
    internal readonly record struct JoinEdit(
        LocalDeclarationStatementSyntax Local,
        ExpressionStatementSyntax Assignment,
        LocalDeclarationStatementSyntax Merged);
}
