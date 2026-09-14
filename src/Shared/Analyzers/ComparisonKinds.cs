// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>Rewrites relational comparison kinds so a rule can reason about one operand order.</summary>
internal static class ComparisonKinds
{
    /// <summary>Returns the comparison that means the same thing with its operands swapped.</summary>
    /// <param name="kind">The comparison kind.</param>
    /// <returns>
    /// The mirrored kind for <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c> and <c>&gt;=</c> — <c>a &lt; b</c> is
    /// <c>b &gt; a</c> — and <paramref name="kind"/> unchanged for every other kind, including the symmetric
    /// <c>==</c> and <c>!=</c>.
    /// </returns>
    internal static SyntaxKind Mirror(SyntaxKind kind) =>
        kind switch
        {
            SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
            SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
            _ => kind,
        };
}
