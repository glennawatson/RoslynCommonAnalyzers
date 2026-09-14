// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports comparisons that place a fixed operand on the left of a variable one.</summary>
internal static class ComparisonOperandOrder
{
    /// <summary>Reports the analyzed comparison when only its left operand matches.</summary>
    /// <param name="context">The syntax node analysis context over a binary comparison.</param>
    /// <param name="matches">Returns whether an operand is the fixed kind that belongs on the right.</param>
    /// <param name="descriptor">The rule to report.</param>
    /// <remarks>Both operands matching is left alone: swapping two fixed operands reads no better.</remarks>
    internal static void ReportWhenOnlyLeftMatches(in SyntaxNodeAnalysisContext context, Func<ExpressionSyntax, bool> matches, DiagnosticDescriptor descriptor)
    {
        var comparison = (BinaryExpressionSyntax)context.Node;
        if (!matches(comparison.Left) || matches(comparison.Right))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(descriptor, comparison.GetLocation()));
    }
}
