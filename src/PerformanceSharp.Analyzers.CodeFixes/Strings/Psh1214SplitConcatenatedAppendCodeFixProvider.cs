// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Splits a reported concatenated <c>StringBuilder</c> append into chained per-part calls
/// (PSH1214): <c>Append(a + b + c)</c> becomes <c>Append(a).Append(b).Append(c)</c> and
/// <c>AppendLine(a + b)</c> becomes <c>Append(a).AppendLine(b)</c> — every part but the
/// last becomes <c>Append</c> and the last keeps the original method. Only the left-nested
/// spine whose <c>+</c> is the non-constant built-in string concatenation is unrolled, so
/// arithmetic prefixes such as <c>i + j + text</c> keep their addition together.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1214SplitConcatenatedAppendCodeFixProvider))]
[Shared]
public sealed class Psh1214SplitConcatenatedAppendCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.SplitConcatenatedAppend.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Split the concatenation into separate Append calls", nameof(Psh1214SplitConcatenatedAppendCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>
    /// Replaces the reported append invocation with its chained per-part form. Benchmark entry
    /// point: the whole left-nested <c>+</c> spine is unrolled syntactically, so the argument's
    /// spine must concatenate strings throughout — the analyzer-driven path re-checks each
    /// level semantically instead.
    /// </summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The reported append invocation.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
    {
        var concatenation = (BinaryExpressionSyntax)invocation.ArgumentList.Arguments[0].Expression;
        return document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation, SyntacticSpineDepth(concatenation))));
    }

    /// <summary>Resolves the reported concatenation and builds the chained per-part replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not BinaryExpressionSyntax concatenation
            || !concatenation.IsKind(SyntaxKind.AddExpression)
            || concatenation.Parent is not ArgumentSyntax { Parent: ArgumentListSyntax { Arguments.Count: 1, Parent: InvocationExpressionSyntax invocation } }
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || access.Name is not IdentifierNameSyntax { Identifier.ValueText: "Append" or "AppendLine" })
        {
            return null;
        }

        var depth = SplittableSpineDepth(model, concatenation);
        return new NodeReplacement(invocation, Rewrite(invocation, depth), current => RewriteCurrent(current, depth));
    }

    /// <summary>Rewrites the current invocation during batch FixAll composition, re-clamping the spine depth.</summary>
    /// <param name="current">The current invocation node, with nested edits already composed.</param>
    /// <param name="depth">The splittable spine depth computed from the original tree.</param>
    /// <returns>The rewritten invocation, or the node unchanged when the shape no longer matches.</returns>
    private static SyntaxNode RewriteCurrent(SyntaxNode current, int depth)
    {
        if (current is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax
            || invocation.ArgumentList.Arguments.Count != 1
            || invocation.ArgumentList.Arguments[0].Expression is not BinaryExpressionSyntax concatenation
            || !concatenation.IsKind(SyntaxKind.AddExpression))
        {
            return current;
        }

        return Rewrite(invocation, Math.Min(depth, SyntacticSpineDepth(concatenation)));
    }

    /// <summary>Builds the chained per-part invocation that replaces the reported one.</summary>
    /// <param name="invocation">The reported append invocation.</param>
    /// <param name="depth">The number of left-spine levels to unroll below the argument's top <c>+</c>.</param>
    /// <returns>The replacement invocation chain.</returns>
    private static InvocationExpressionSyntax Rewrite(InvocationExpressionSyntax invocation, int depth)
    {
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        var operands = CollectOperands((BinaryExpressionSyntax)invocation.ArgumentList.Arguments[0].Expression, depth);

        var chain = access.Expression;
        for (var index = 0; index < operands.Length - 1; index++)
        {
            chain = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    chain,
                    SyntaxFactory.IdentifierName("Append")),
                SingleArgumentList(operands[index]));
        }

        return invocation
            .WithExpression(access.WithExpression(chain))
            .WithArgumentList(SingleArgumentList(operands[operands.Length - 1]).WithTriviaFrom(invocation.ArgumentList))
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }

    /// <summary>Flattens the argument's left-nested spine into its ordered operands.</summary>
    /// <param name="concatenation">The argument's top <c>+</c> expression.</param>
    /// <param name="depth">The number of left-spine levels to unroll below the top <c>+</c>.</param>
    /// <returns>The operands in source order; parenthesized operands keep their parentheses.</returns>
    private static ExpressionSyntax[] CollectOperands(BinaryExpressionSyntax concatenation, int depth)
    {
        var operands = new ExpressionSyntax[depth + 2];
        var current = concatenation;
        for (var index = depth + 1; index >= 1; index--)
        {
            operands[index] = current.Right;
            if (index > 1)
            {
                current = (BinaryExpressionSyntax)current.Left;
            }
        }

        operands[0] = current.Left;
        return operands;
    }

    /// <summary>Counts the left-spine levels whose <c>+</c> is a non-constant built-in string concatenation.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="concatenation">The argument's top <c>+</c> expression.</param>
    /// <returns>The number of splittable levels below the top <c>+</c>.</returns>
    private static int SplittableSpineDepth(SemanticModel model, BinaryExpressionSyntax concatenation)
    {
        var depth = 0;
        var current = concatenation;
        while (current.Left is BinaryExpressionSyntax left
            && left.IsKind(SyntaxKind.AddExpression)
            && IsSplittableConcatenation(model, left))
        {
            depth++;
            current = left;
        }

        return depth;
    }

    /// <summary>Counts the left-spine levels that are syntactic <c>+</c> expressions.</summary>
    /// <param name="concatenation">The argument's top <c>+</c> expression.</param>
    /// <returns>The number of <c>+</c> levels below the top <c>+</c>.</returns>
    private static int SyntacticSpineDepth(BinaryExpressionSyntax concatenation)
    {
        var depth = 0;
        var current = concatenation;
        while (current.Left is BinaryExpressionSyntax left && left.IsKind(SyntaxKind.AddExpression))
        {
            depth++;
            current = left;
        }

        return depth;
    }

    /// <summary>
    /// Returns whether a spine <c>+</c> can be unrolled: it must be the built-in string
    /// concatenation (an arithmetic prefix such as <c>i + j</c> must stay one operand) and
    /// not a compile-time constant (a constant subchain stays folded by the compiler).
    /// </summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="concatenation">The candidate spine <c>+</c> expression.</param>
    /// <returns><see langword="true"/> when the operands can become separate appends.</returns>
    private static bool IsSplittableConcatenation(SemanticModel model, BinaryExpressionSyntax concatenation)
        => model.GetSymbolInfo(concatenation).Symbol is IMethodSymbol { MethodKind: MethodKind.BuiltinOperator, ContainingType.SpecialType: SpecialType.System_String }
            && !model.GetConstantValue(concatenation).HasValue;

    /// <summary>Wraps one operand, stripped of outer trivia, as a single-argument list.</summary>
    /// <param name="operand">The operand to pass as the argument.</param>
    /// <returns>The argument list.</returns>
    private static ArgumentListSyntax SingleArgumentList(ExpressionSyntax operand)
        => SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(operand.WithoutTrivia())));
}
