// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Works out the indentation a code fix writes a new member of a type at.</summary>
internal static class MemberIndentation
{
    /// <summary>Gets the indentation for a member of a type: the type's own indentation plus one level.</summary>
    /// <param name="owner">The type receiving the member.</param>
    /// <returns>The member indentation whitespace.</returns>
    internal static string Of(BaseTypeDeclarationSyntax owner)
    {
        var leading = owner.GetLeadingTrivia();
        var typeIndent = leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? leading[leading.Count - 1].ToString()
            : string.Empty;
        return $"{typeIndent}    ";
    }
}
