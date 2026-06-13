// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites <c>x = x op y</c> as the compound assignment <c>x op= y</c> (SST1185).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseCompoundAssignmentCodeFixProvider))]
[Shared]
public sealed class UseCompoundAssignmentCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseCompoundAssignment.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not AssignmentExpressionSyntax assignment
                || assignment.Right is not BinaryExpressionSyntax binary
                || !CompoundAssignmentOperators.TryMap(binary.Kind(), out _, out _, out _))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use a compound assignment",
                    _ => Task.FromResult(Apply(context.Document, root, assignment, binary)),
                    equivalenceKey: nameof(UseCompoundAssignmentCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Collapses the self-recomputing assignment into its compound-operator form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="assignment">The <c>x = x op y</c> assignment.</param>
    /// <param name="binary">The right-hand-side binary expression.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, AssignmentExpressionSyntax assignment, BinaryExpressionSyntax binary)
    {
        CompoundAssignmentOperators.TryMap(binary.Kind(), out var assignmentKind, out var operatorToken, out _);

        // Reuse the original '=' spacing for the compound operator so 'x = ...' becomes 'x op= ...'.
        var equals = assignment.OperatorToken;
        var compoundOperator = SyntaxFactory.Token(equals.LeadingTrivia, operatorToken, equals.TrailingTrivia);
        var replacement = SyntaxFactory.AssignmentExpression(assignmentKind, assignment.Left, compoundOperator, binary.Right.WithLeadingTrivia(SyntaxFactory.TriviaList()));

        return document.WithSyntaxRoot(root.ReplaceNode(assignment, replacement.WithTriviaFrom(assignment)));
    }
}
