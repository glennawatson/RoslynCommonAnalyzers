// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an embedded statement block whose closing brace is directly followed by another
/// statement with no blank line between them (SST1513). Closing braces that are followed by
/// a chained keyword (<c>else</c>, <c>catch</c>, <c>finally</c>, <c>while</c>), by another
/// closing brace, by a statement terminator, or by end of file are exempt; element-body
/// closing braces are governed by SST1516.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1513ClosingBraceSpacingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.CloseBraceFollowedByBlankLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Block);
    }

    /// <summary>Reports a statement block's closing brace that is not followed by a blank line.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var block = (BlockSyntax)context.Node;
        if (IsElementOrFunctionBody(block.Parent))
        {
            return;
        }

        var close = block.CloseBraceToken;
        var next = close.GetNextToken();
        if (IsExemptFollower(next))
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        if (LayoutHelpers.StartLine(text, next) != LayoutHelpers.StartLine(text, close) + 1)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.CloseBraceFollowedByBlankLine, close.GetLocation()));
    }

    /// <summary>Returns whether the block is the body of a member, accessor, local function, or lambda.</summary>
    /// <param name="parent">The block's parent node.</param>
    /// <returns><see langword="true"/> when the block is an element or function body.</returns>
    private static bool IsElementOrFunctionBody(SyntaxNode? parent)
        => parent is BaseMethodDeclarationSyntax
            or AccessorDeclarationSyntax
            or AnonymousFunctionExpressionSyntax
            or LocalFunctionStatementSyntax;

    /// <summary>Returns whether the token following the closing brace exempts it from needing a blank line.</summary>
    /// <param name="next">The token after the closing brace.</param>
    /// <returns><see langword="true"/> when no blank line is required.</returns>
    private static bool IsExemptFollower(SyntaxToken next)
        => next.IsKind(SyntaxKind.None)
            || next.IsKind(SyntaxKind.CloseBraceToken)
            || next.IsKind(SyntaxKind.ElseKeyword)
            || next.IsKind(SyntaxKind.CatchKeyword)
            || next.IsKind(SyntaxKind.FinallyKeyword)
            || next.IsKind(SyntaxKind.WhileKeyword)
            || next.IsKind(SyntaxKind.SemicolonToken);
}
