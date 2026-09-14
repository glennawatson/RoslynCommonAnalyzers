// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Swaps a comparison so the literal sits on the right (SST1186).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LiteralOnRightOfComparisonCodeFixProvider))]
[Shared]
public sealed class LiteralOnRightOfComparisonCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.LiteralOnRightOfComparison.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Place the literal on the right",
            nameof(LiteralOnRightOfComparisonCodeFixProvider),
            ReportedNode.Is<BinaryExpressionSyntax>,
            TryRewrite);

    /// <summary>Resolves the reported comparison and swaps its operands, keeping the surrounding spacing in place.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not BinaryExpressionSyntax comparison)
        {
            return null;
        }

        var newLeft = comparison.Right.WithTriviaFrom(comparison.Left);
        var newRight = comparison.Left.WithTriviaFrom(comparison.Right);
        return new NodeReplacement(comparison, comparison.Update(newLeft, comparison.OperatorToken, newRight));
    }
}
