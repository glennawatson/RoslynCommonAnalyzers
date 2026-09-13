// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes the redundant operand of a flags combination reported by SST2495, dropping it and its
/// adjacent <c>|</c> so the surviving operands still compute the same value. The operand's immediate
/// <c>|</c> operation is replaced by whichever side survives, carrying that operation's outer trivia.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2495RedundantFlagsOperandCodeFixProvider))]
[Shared]
public sealed class Sst2495RedundantFlagsOperandCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.RedundantFlagsOperand.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Remove the redundant operand",
            nameof(Sst2495RedundantFlagsOperandCodeFixProvider),
            CanRewrite,
            TryRewrite);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var element = root.FindNode(diagnostic.Location.SourceSpan);
        if (element is null)
        {
            return false;
        }

        while (element.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            element = parenthesized;
        }

        return element.Parent is BinaryExpressionSyntax operation
            && operation.IsKind(SyntaxKind.BitwiseOrExpression);
    }

    /// <summary>Resolves the reported operand and replaces its <c>|</c> operation with the surviving side.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var element = root.FindNode(diagnostic.Location.SourceSpan);
        if (element is null)
        {
            return null;
        }

        while (element.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            element = parenthesized;
        }

        if (element.Parent is not BinaryExpressionSyntax operation || !operation.IsKind(SyntaxKind.BitwiseOrExpression))
        {
            return null;
        }

        var replacement = operation.Right == element
            ? operation.Left.WithTrailingTrivia(operation.GetTrailingTrivia())
            : operation.Right.WithLeadingTrivia(operation.GetLeadingTrivia());
        return new NodeReplacement(operation, replacement);
    }
}
