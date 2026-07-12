// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a bitwise operation that cannot change its operand's value (SST1481): <c>x | 0</c>,
/// <c>x ^ 0</c> and <c>x &amp; ~0</c> all collapse to <c>x</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>x &amp; 0</c> is reported without a fix. It is always <c>0</c>, but whether the author meant a
/// different mask or meant to delete the expression is a question only they can answer, and rewriting it to
/// <c>0</c> would quietly bless the bug. The analyzer withholds the surviving-operand property in that
/// case, and this fix stays out of the way.
/// </para>
/// <para>
/// The surviving operand never needs parentheses added: it was already a direct operand of the removed
/// operator, so it binds at least as tightly as that operator did, and taking the operator's place cannot
/// change how anything around it groups. Parentheses that already wrapped the operation are only dropped
/// when what survives is a single primary expression, which nothing can regroup — <c>a &amp; (b | 0)</c>
/// becomes <c>a &amp; b</c>, while <c>a &amp; (b ^ c | 0)</c> keeps them and becomes
/// <c>a &amp; (b ^ c)</c>, because <c>a &amp; b ^ c</c> would mean something else entirely.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1481RedundantBitwiseOperationCodeFixProvider))]
[Shared]
public sealed class Sst1481RedundantBitwiseOperationCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(MaintainabilityRules.RedundantBitwiseOperation.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Remove the redundant operation",
            nameof(Sst1481RedundantBitwiseOperationCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Applies one SST1481 removal for the reported operation.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original document when the diagnostic no longer resolves.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
        => TryRewrite(root, diagnostic) is { } edit
            ? document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))
            : document;

    /// <summary>Resolves the reported operation and lifts out the operand that survives it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the diagnostic carries no fix.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (!diagnostic.Properties.TryGetValue(Sst1481RedundantBitwiseOperationAnalyzer.SurvivingOperandKey, out var side)
            || side is not (Sst1481RedundantBitwiseOperationAnalyzer.LeftOperandSurvives
                or Sst1481RedundantBitwiseOperationAnalyzer.RightOperandSurvives))
        {
            return null;
        }

        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not BinaryExpressionSyntax binary
            || !IsFixableKind(binary))
        {
            return null;
        }

        var keepLeft = side == Sst1481RedundantBitwiseOperationAnalyzer.LeftOperandSurvives;
        if (binary.Parent is ParenthesizedExpressionSyntax parentheses && IsPrimary(Surviving(binary, keepLeft)))
        {
            return new NodeReplacement(
                parentheses,
                Surviving(binary, keepLeft).WithTriviaFrom(parentheses),
                current => Collapse(current, keepLeft));
        }

        return new NodeReplacement(
            binary,
            Surviving(binary, keepLeft).WithTriviaFrom(binary),
            current => Lift(current, keepLeft));
    }

    /// <summary>Lifts the surviving operand out of the operation once nested edits have been composed.</summary>
    /// <param name="current">The operation, as it stands after any nested fix.</param>
    /// <param name="keepLeft">Whether the left operand is the one that survives.</param>
    /// <returns>The surviving operand, or the node unchanged when it no longer matches.</returns>
    private static SyntaxNode Lift(SyntaxNode current, bool keepLeft)
        => current is BinaryExpressionSyntax binary && IsFixableKind(binary)
            ? Surviving(binary, keepLeft).WithTriviaFrom(binary)
            : current;

    /// <summary>Lifts the surviving operand out of the operation and its now-pointless parentheses.</summary>
    /// <param name="current">The parenthesized operation, as it stands after any nested fix.</param>
    /// <param name="keepLeft">Whether the left operand is the one that survives.</param>
    /// <returns>The surviving operand, or the node unchanged when it no longer matches.</returns>
    private static SyntaxNode Collapse(SyntaxNode current, bool keepLeft)
        => current is ParenthesizedExpressionSyntax { Expression: BinaryExpressionSyntax binary } parentheses && IsFixableKind(binary)
            ? Surviving(binary, keepLeft).WithTriviaFrom(parentheses)
            : current;

    /// <summary>Gets the operand the operation leaves untouched.</summary>
    /// <param name="binary">The reported operation.</param>
    /// <param name="keepLeft">Whether the left operand is the one that survives.</param>
    /// <returns>The surviving operand.</returns>
    private static ExpressionSyntax Surviving(BinaryExpressionSyntax binary, bool keepLeft)
        => keepLeft ? binary.Left : binary.Right;

    /// <summary>Returns whether an expression is a primary one that no surrounding operator can regroup.</summary>
    /// <param name="expression">The surviving operand.</param>
    /// <returns><see langword="true"/> when the operand is safe to unwrap out of its parentheses.</returns>
    private static bool IsPrimary(ExpressionSyntax expression)
        => expression is IdentifierNameSyntax
            or LiteralExpressionSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or ParenthesizedExpressionSyntax
            or ThisExpressionSyntax;

    /// <summary>Returns whether the resolved node is still one of the reported operator kinds.</summary>
    /// <param name="binary">The candidate expression.</param>
    /// <returns><see langword="true"/> for <c>&amp;</c>, <c>|</c> and <c>^</c> expressions.</returns>
    private static bool IsFixableKind(BinaryExpressionSyntax binary)
        => binary.RawKind is (int)SyntaxKind.BitwiseOrExpression
            or (int)SyntaxKind.ExclusiveOrExpression
            or (int)SyntaxKind.BitwiseAndExpression;
}
