// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a written type name into the expression the parser would have produced for the same text.
/// </summary>
/// <remarks>
/// A code fix that turns <c>new System.Random()</c> into <c>System.Random.Shared</c> is tempted to reuse the
/// <see cref="QualifiedNameSyntax"/> it already has: it is an <see cref="ExpressionSyntax"/>, and the result
/// even compiles. It is still the wrong tree. <c>System.Random</c> written in an <em>expression</em> is a
/// chain of <c>SimpleMemberAccessExpression</c> nodes, and a <see cref="QualifiedNameSyntax"/> is what the
/// parser builds in a <em>type</em> position only — so the fixed document would not match the document the
/// compiler produces from the same source text. Converting the name keeps the two identical.
/// </remarks>
internal static class TypeNameExpression
{
    /// <summary>Converts a written type name to its expression form.</summary>
    /// <param name="name">The type name as the author wrote it.</param>
    /// <returns>The equivalent expression.</returns>
    /// <remarks>
    /// An alias qualifier (<c>global::System</c>) is already an expression in its own right, and is left as
    /// the innermost receiver of the chain rather than taken apart.
    /// </remarks>
    public static ExpressionSyntax From(NameSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            From(qualified.Left),
            qualified.Right),
        _ => name,
    };
}
