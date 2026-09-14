// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Finds the binary expression a diagnostic was reported on, walking out from the reported node.</summary>
internal static class EnclosingBinaryExpression
{
    /// <summary>Finds the nearest binary expression of either of two kinds at or around the diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="kind">One accepted binary expression kind.</param>
    /// <param name="alternateKind">The other accepted binary expression kind.</param>
    /// <returns>The nearest matching binary expression, or <see langword="null"/> when no ancestor matches.</returns>
    internal static BinaryExpressionSyntax? Find(SyntaxNode root, Diagnostic diagnostic, SyntaxKind kind, SyntaxKind alternateKind)
    {
        for (var current = root.FindNode(diagnostic.Location.SourceSpan); current is not null; current = current.Parent)
        {
            if (current is BinaryExpressionSyntax binary && (binary.IsKind(kind) || binary.IsKind(alternateKind)))
            {
                return binary;
            }
        }

        return null;
    }
}
