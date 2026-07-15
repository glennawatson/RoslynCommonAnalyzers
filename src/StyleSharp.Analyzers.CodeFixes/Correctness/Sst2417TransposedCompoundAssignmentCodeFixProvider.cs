// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Offers the two readings of a transposed-operator assignment (SST2417), neither preferred: close the gap
/// into the compound operator (<c>x += 1</c>), or open it so the unary value is plain (<c>x = +1</c>).
/// </summary>
/// <remarks>
/// Only the author knows which was meant, so both are offered and neither is the default. The compound
/// reading is offered only for <c>+</c> and <c>-</c>, where <c>+=</c> / <c>-=</c> exist; for <c>!</c> only
/// the unary reading is meaningful.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2417TransposedCompoundAssignmentCodeFixProvider))]
[Shared]
public sealed class Sst2417TransposedCompoundAssignmentCodeFixProvider : CodeFixProvider
{
    /// <summary>The equivalence key for the compound-operator reading.</summary>
    private const string CompoundKey = nameof(Sst2417TransposedCompoundAssignmentCodeFixProvider) + ".Compound";

    /// <summary>The equivalence key for the unary-value reading.</summary>
    private const string UnaryKey = nameof(Sst2417TransposedCompoundAssignmentCodeFixProvider) + ".Unary";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.TransposedCompoundAssignment.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<AssignmentExpressionSyntax>() is not { } assignment
                || !TransposedCompoundAssignment.Matches(assignment)
                || assignment.Right is not PrefixUnaryExpressionSyntax prefix)
            {
                continue;
            }

            RegisterUnaryReading(context, root, assignment, prefix, diagnostic);
            RegisterCompoundReading(context, root, assignment, prefix, diagnostic);
        }
    }

    /// <summary>Registers the "assign the unary value" reading, spacing the operator as a unary sign.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="assignment">The reported assignment.</param>
    /// <param name="prefix">The assignment's unary right operand.</param>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    private static void RegisterUnaryReading(
        CodeFixContext context,
        SyntaxNode root,
        AssignmentExpressionSyntax assignment,
        PrefixUnaryExpressionSyntax prefix,
        Diagnostic diagnostic)
    {
        var rewritten = assignment
            .WithOperatorToken(assignment.OperatorToken.WithTrailingTrivia(SyntaxFactory.Space))
            .WithRight(prefix.WithOperatorToken(prefix.OperatorToken.WithTrailingTrivia()));
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Assign the unary value ('= {prefix.OperatorToken.Text}')",
                _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(assignment, rewritten))),
                UnaryKey),
            diagnostic);
    }

    /// <summary>Registers the "use the compound operator" reading, for <c>+</c> and <c>-</c> only.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="assignment">The reported assignment.</param>
    /// <param name="prefix">The assignment's unary right operand.</param>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    private static void RegisterCompoundReading(
        CodeFixContext context,
        SyntaxNode root,
        AssignmentExpressionSyntax assignment,
        PrefixUnaryExpressionSyntax prefix,
        Diagnostic diagnostic)
    {
        var compound = Compound(prefix.Kind());
        if (compound is null)
        {
            return;
        }

        var (compoundKind, tokenKind) = compound.Value;
        var operatorToken = SyntaxFactory.Token(assignment.OperatorToken.LeadingTrivia, tokenKind, prefix.OperatorToken.TrailingTrivia);
        var rewritten = SyntaxFactory.AssignmentExpression(compoundKind, assignment.Left, operatorToken, prefix.Operand);
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Use the compound operator ('{prefix.OperatorToken.Text}=')",
                _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(assignment, rewritten))),
                CompoundKey),
            diagnostic);
    }

    /// <summary>Maps a unary operator to its compound-assignment kind and token, when one exists.</summary>
    /// <param name="unaryKind">The unary expression kind.</param>
    /// <returns>The compound kind and token, or <see langword="null"/> for <c>!</c>.</returns>
    private static (SyntaxKind CompoundKind, SyntaxKind TokenKind)? Compound(SyntaxKind unaryKind) => unaryKind switch
    {
        SyntaxKind.UnaryPlusExpression => (SyntaxKind.AddAssignmentExpression, SyntaxKind.PlusEqualsToken),
        SyntaxKind.UnaryMinusExpression => (SyntaxKind.SubtractAssignmentExpression, SyntaxKind.MinusEqualsToken),
        _ => null,
    };
}
