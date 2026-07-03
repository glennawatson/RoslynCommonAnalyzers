// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Locates representative syntax nodes within synthetic code-fix benchmark corpora.</summary>
internal static class CodeFixBenchmarkSyntaxLookup
{
    /// <summary>Returns the Nth member of the first namespace declaration.</summary>
    /// <typeparam name="T">The member type to locate.</typeparam>
    /// <param name="root">The compilation-unit root.</param>
    /// <param name="index">The zero-based match index.</param>
    /// <returns>The matching member.</returns>
    public static T GetNthNamespaceMember<T>(CompilationUnitSyntax root, int index)
        where T : MemberDeclarationSyntax
        => GetNthMember<T>(((BaseNamespaceDeclarationSyntax)root.Members[0]).Members, index);

    /// <summary>Returns the Nth member of the first type declared in the first namespace.</summary>
    /// <typeparam name="T">The member type to locate.</typeparam>
    /// <param name="root">The compilation-unit root.</param>
    /// <param name="index">The zero-based match index.</param>
    /// <returns>The matching member.</returns>
    public static T GetNthTypeMember<T>(CompilationUnitSyntax root, int index)
        where T : MemberDeclarationSyntax
        => GetNthMember<T>(((TypeDeclarationSyntax)((BaseNamespaceDeclarationSyntax)root.Members[0]).Members[0]).Members, index);

    /// <summary>Returns the Nth descendant node matching a predicate.</summary>
    /// <typeparam name="T">The node type to locate.</typeparam>
    /// <param name="root">The root node to search.</param>
    /// <param name="index">The zero-based match index.</param>
    /// <param name="predicate">The filter applied to candidate nodes.</param>
    /// <returns>The matching descendant node.</returns>
    public static T GetNthDescendant<T>(SyntaxNode root, int index, Func<T, bool> predicate)
        where T : SyntaxNode
    {
        var current = 0;
        foreach (var node in root.DescendantNodes())
        {
            if (node is not T typed || !predicate(typed))
            {
                continue;
            }

            if (current == index)
            {
                return typed;
            }

            current++;
        }

        throw new InvalidOperationException($"Unable to locate descendant {typeof(T).Name} at index {index}.");
    }

    /// <summary>Returns the Nth member of a syntax list filtered by type.</summary>
    /// <typeparam name="T">The member type to locate.</typeparam>
    /// <param name="members">The candidate members.</param>
    /// <param name="index">The zero-based match index.</param>
    /// <returns>The matching member.</returns>
    private static T GetNthMember<T>(SyntaxList<MemberDeclarationSyntax> members, int index)
        where T : MemberDeclarationSyntax
    {
        var current = 0;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i] is not T typed)
            {
                continue;
            }

            if (current == index)
            {
                return typed;
            }

            current++;
        }

        throw new InvalidOperationException($"Unable to locate member {typeof(T).Name} at index {index}.");
    }
}
