// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>Resolves the node a diagnostic reports, for code fixes that check applicability before building any syntax.</summary>
internal static class ReportedNode
{
    /// <summary>Returns whether the node at a diagnostic's span has the expected type.</summary>
    /// <typeparam name="T">The node type the diagnostic reports.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns><see langword="true"/> when the reported node is a <typeparamref name="T"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Is<T>(SyntaxNode root, Diagnostic diagnostic)
        where T : SyntaxNode =>
        root.FindNode(diagnostic.Location.SourceSpan) is T;

    /// <summary>Returns the node at a diagnostic's span when it has the expected type.</summary>
    /// <typeparam name="T">The node type the diagnostic reports.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The reported node, or <see langword="null"/> when it has another type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T? Find<T>(SyntaxNode root, Diagnostic diagnostic)
        where T : SyntaxNode =>
        root.FindNode(diagnostic.Location.SourceSpan) as T;

    /// <summary>Returns the nearest node of the expected type enclosing a diagnostic's span, starting at the reported node.</summary>
    /// <typeparam name="T">The enclosing node type.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The enclosing node, or <see langword="null"/> when there is none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T? Ancestor<T>(SyntaxNode root, Diagnostic diagnostic)
        where T : SyntaxNode =>
        root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<T>();
}
