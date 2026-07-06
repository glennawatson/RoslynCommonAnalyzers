// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported <c>Enum.HasFlag</c> call to a bitwise flag test (PSH1016):
/// <c>x.HasFlag(F)</c> becomes <c>(x &amp; F) == F</c>, and a directly negated
/// <c>!x.HasFlag(F)</c> becomes <c>(x &amp; F) != F</c> by replacing the whole prefix-unary
/// node. The flag operand appears twice in the rewrite, so the fix is only offered when
/// re-evaluating it is safe — identifiers, member access chains, literals, and parenthesized
/// or bitwise-or combinations of those; any other argument keeps the diagnostic without a
/// fix. A bitwise-or argument is parenthesized on both sides so operator precedence is
/// preserved: <c>x.HasFlag(A | B)</c> becomes <c>(x &amp; (A | B)) == (A | B)</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1016UseBitwiseFlagTestCodeFixProvider))]
[Shared]
public sealed class Psh1016UseBitwiseFlagTestCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.UseBitwiseFlagTest.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use a bitwise flag test", nameof(Psh1016UseBitwiseFlagTestCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Applies the bitwise rewrite to one HasFlag invocation.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The HasFlag invocation to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
        => TryGetReplacement(invocation) is { } edit
            ? document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))
            : document;

    /// <summary>Resolves the reported HasFlag invocation and builds its bitwise replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches or the flag argument is not safe to repeat.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is InvocationExpressionSyntax invocation
            ? TryGetReplacement(invocation)
            : null;

    /// <summary>Builds the bitwise-test edit for one HasFlag invocation.</summary>
    /// <param name="invocation">The HasFlag invocation to rewrite.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape does not match or the flag argument is not safe to repeat.</returns>
    private static NodeReplacement? TryGetReplacement(InvocationExpressionSyntax invocation)
    {
        if (Psh1016UseBitwiseFlagTestAnalyzer.TryGetHasFlagAccess(invocation) is not { } access)
        {
            return null;
        }

        var flag = invocation.ArgumentList.Arguments[0].Expression;
        if (!IsRepeatSafe(flag))
        {
            return null;
        }

        var negated = invocation.Parent is PrefixUnaryExpressionSyntax parent && parent.IsKind(SyntaxKind.LogicalNotExpression);
        SyntaxNode original = negated ? invocation.Parent! : invocation;
        return new NodeReplacement(original, Rewrite(access.Expression, flag, negated, original));
    }

    /// <summary>Builds the bitwise comparison that replaces the reported node.</summary>
    /// <param name="receiver">The enum value the flag is tested on.</param>
    /// <param name="flag">The flag argument, repeated on both sides of the comparison.</param>
    /// <param name="negated">Whether a folded logical-not turns the test into <c>!=</c>.</param>
    /// <param name="original">The node being replaced, supplying trivia and the precedence context.</param>
    /// <returns>The replacement expression carrying the original node's trivia.</returns>
    private static ExpressionSyntax Rewrite(ExpressionSyntax receiver, ExpressionSyntax flag, bool negated, SyntaxNode original)
    {
        var masked = SyntaxFactory.ParenthesizedExpression(
            SyntaxFactory.BinaryExpression(SyntaxKind.BitwiseAndExpression, receiver.WithoutTrivia(), ParenthesizeIfNeeded(flag)));
        ExpressionSyntax result = SyntaxFactory.BinaryExpression(
            negated ? SyntaxKind.NotEqualsExpression : SyntaxKind.EqualsExpression,
            masked,
            ParenthesizeIfNeeded(flag));

        if (NeedsParentheses(original))
        {
            result = SyntaxFactory.ParenthesizedExpression(result);
        }

        // NormalizeWhitespace strips the elastic trivia the SyntaxFactory nodes carry so the
        // comparison stays on one line with conventional spacing.
        return result.NormalizeWhitespace()
            .WithTriviaFrom(original)
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }

    /// <summary>Returns the flag operand, parenthesized when its precedence is below a comparison operand's.</summary>
    /// <param name="flag">The flag argument to reuse.</param>
    /// <returns>The trivia-free operand; a bitwise-or combination gains parentheses, the other safe shapes need none.</returns>
    private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax flag)
        => flag.IsKind(SyntaxKind.BitwiseOrExpression)
            ? SyntaxFactory.ParenthesizedExpression(flag.WithoutTrivia())
            : flag.WithoutTrivia();

    /// <summary>Returns whether evaluating an expression twice cannot change behavior or trigger side effects.</summary>
    /// <param name="expression">The candidate flag argument.</param>
    /// <returns><see langword="true"/> for identifiers, member access chains, literals, and parenthesized or bitwise-or combinations of those.</returns>
    private static bool IsRepeatSafe(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierNameSyntax => true,
            LiteralExpressionSyntax => true,
            ThisExpressionSyntax => true,
            BaseExpressionSyntax => true,
            MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression) => IsRepeatSafe(access.Expression),
            ParenthesizedExpressionSyntax parenthesized => IsRepeatSafe(parenthesized.Expression),
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.BitwiseOrExpression) => IsRepeatSafe(binary.Left) && IsRepeatSafe(binary.Right),
            _ => false,
        };

    /// <summary>Returns whether the replacement comparison must be parenthesized in the original node's context.</summary>
    /// <param name="original">The node being replaced.</param>
    /// <returns><see langword="true"/> when the parent is an enclosing expression rather than a statement, argument, clause, or assignment-value position.</returns>
    private static bool NeedsParentheses(SyntaxNode original)
    {
        if (original.Parent is not ExpressionSyntax parent || parent is ParenthesizedExpressionSyntax)
        {
            return false;
        }

        return parent is not AssignmentExpressionSyntax assignment || assignment.Right != original;
    }
}
