// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Folds a null guard into the following assignment as a throw expression (SST2283): the guard is
/// removed and its throw is moved onto the assigned value —
/// <c>if (x is null) throw new SomeException(); _x = x;</c> becomes
/// <c>_x = x ?? throw new SomeException();</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2283FoldGuardIntoAssignedValueCodeFixProvider))]
[Shared]
public sealed class Sst2283FoldGuardIntoAssignedValueCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(ModernSyntaxRules.FoldGuardIntoAssignedValue.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Fold the guard into a throw expression", nameof(Sst2283FoldGuardIntoAssignedValueCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported guard and rewrites its block with the guard removed and the throw folded.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The block replacement, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        // The reported diagnostic already honored the argument-null stand-down, so re-derivation need not
        // re-check it: any reported guard passed that gate in this same compilation.
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<IfStatementSyntax>() is not { } ifStatement
            || ifStatement.Parent is not BlockSyntax block
            || !Sst2283FoldGuardIntoAssignedValueAnalyzer.TryGetFold(
                ifStatement,
                model,
                argumentNullFolded: false,
                CancellationToken.None,
                out var guardedValue,
                out var throwOperand,
                out var assignmentStatement)
            || assignmentStatement.Expression is not AssignmentExpressionSyntax assignment)
        {
            return null;
        }

        var coalesce = SyntaxFactory.BinaryExpression(
            SyntaxKind.CoalesceExpression,
            guardedValue.WithoutTrivia(),
            SyntaxFactory.ThrowExpression(throwOperand.WithoutTrivia()));

        // Take the guard's own trivia so the folded statement lands where the guard stood, then drop the
        // original assignment; any blank line that separated the two leaves with it.
        var foldedStatement = assignmentStatement.WithExpression(assignment.WithRight(coalesce)).WithTriviaFrom(ifStatement);
        var index = block.Statements.IndexOf(ifStatement);
        var statements = block.Statements.Replace(ifStatement, foldedStatement).RemoveAt(index + 1);
        return new NodeReplacement(block, block.WithStatements(statements));
    }
}
