// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Finds the node a diagnostic was reported on by walking up from the node that covers its span.</summary>
internal static class DiagnosticEnclosingNode
{
    /// <summary>Finds the nearest node of one type that is, or encloses, the node covering a diagnostic's span.</summary>
    /// <typeparam name="T">The node type to find.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nearest node of that type, or <see langword="null"/> when there is none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T? Find<T>(SyntaxNode root, Diagnostic diagnostic)
        where T : SyntaxNode =>
        root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<T>();
}
