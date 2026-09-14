// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites <c>x = x op y</c> as the compound assignment <c>x op= y</c> (SST1185).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseCompoundAssignmentCodeFixProvider))]
[Shared]
public sealed class UseCompoundAssignmentCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseCompoundAssignment.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use a compound assignment",
            nameof(UseCompoundAssignmentCodeFixProvider),
            static (root, diagnostic) => TryResolve(root, diagnostic, out _, out _, out _, out _),
            TryRewrite);

    /// <summary>Resolves the reported assignment and builds its compound-operator form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryResolve(root, diagnostic, out var assignment, out var binary, out var assignmentKind, out var operatorToken)
            ? new NodeReplacement(assignment, CreateCompound(assignment, binary, assignmentKind, operatorToken))
            : null;

    /// <summary>Resolves the reported <c>x = x op y</c> assignment, when its operator has a compound form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="assignment">The <c>x = x op y</c> assignment.</param>
    /// <param name="binary">The right-hand-side binary expression.</param>
    /// <param name="assignmentKind">The compound assignment expression kind.</param>
    /// <param name="operatorToken">The compound operator token kind.</param>
    /// <returns><see langword="true"/> when the reported shape still has a compound form.</returns>
    private static bool TryResolve(
        SyntaxNode root,
        Diagnostic diagnostic,
        [NotNullWhen(true)] out AssignmentExpressionSyntax? assignment,
        [NotNullWhen(true)] out BinaryExpressionSyntax? binary,
        out SyntaxKind assignmentKind,
        out SyntaxKind operatorToken)
    {
        assignmentKind = default;
        operatorToken = default;
        if (root.FindNode(diagnostic.Location.SourceSpan) is not AssignmentExpressionSyntax { Right: BinaryExpressionSyntax right } reported)
        {
            assignment = null;
            binary = null;
            return false;
        }

        assignment = reported;
        binary = right;
        return CompoundAssignmentOperators.TryMap(right.Kind(), out assignmentKind, out operatorToken, out _);
    }

    /// <summary>Builds the compound assignment that takes the original assignment's place and trivia.</summary>
    /// <param name="assignment">The <c>x = x op y</c> assignment.</param>
    /// <param name="binary">The right-hand-side binary expression.</param>
    /// <param name="assignmentKind">The compound assignment expression kind.</param>
    /// <param name="operatorToken">The compound operator token kind.</param>
    /// <returns>The compound assignment.</returns>
    private static AssignmentExpressionSyntax CreateCompound(
        AssignmentExpressionSyntax assignment,
        BinaryExpressionSyntax binary,
        SyntaxKind assignmentKind,
        SyntaxKind operatorToken)
    {
        // Reuse the original '=' spacing for the compound operator so 'x = ...' becomes 'x op= ...'.
        var equals = assignment.OperatorToken;
        var compoundOperator = SyntaxFactory.Token(equals.LeadingTrivia, operatorToken, equals.TrailingTrivia);
        var replacement = SyntaxFactory.AssignmentExpression(assignmentKind, assignment.Left, compoundOperator, binary.Right.WithLeadingTrivia(SyntaxFactory.TriviaList()));
        return replacement.WithTriviaFrom(assignment);
    }
}
