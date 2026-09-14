// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports a rule on one token of the analyzed node when the node matches the rule's syntactic test.</summary>
internal static class NodeTokenReport
{
    /// <summary>Reports <paramref name="descriptor"/> at the token <paramref name="token"/> selects when the node matches.</summary>
    /// <typeparam name="TNode">The node type the action is registered for.</typeparam>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="matches">The rule's syntactic test.</param>
    /// <param name="token">Selects the token the diagnostic covers; called only for a match.</param>
    /// <param name="descriptor">The rule to report.</param>
    internal static void WhenMatches<TNode>(
        in SyntaxNodeAnalysisContext context,
        Func<TNode, bool> matches,
        Func<TNode, SyntaxToken> token,
        DiagnosticDescriptor descriptor)
        where TNode : SyntaxNode
    {
        var node = (TNode)context.Node;
        if (!matches(node))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(descriptor, token(node).GetLocation()));
    }
}
