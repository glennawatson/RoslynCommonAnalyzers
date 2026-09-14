// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Finds the invocation a diagnostic was reported on, walking out from the reported node.</summary>
internal static class EnclosingInvocation
{
    /// <summary>Finds the nearest invocation at or around the diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nearest invocation, or <see langword="null"/> when no ancestor is one.</returns>
    internal static InvocationExpressionSyntax? Find(SyntaxNode root, Diagnostic diagnostic)
    {
        for (var current = root.FindNode(diagnostic.Location.SourceSpan); current is not null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation)
            {
                return invocation;
            }
        }

        return null;
    }
}
