// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Recognizes <c>System.ReadOnlySpan&lt;T&gt;</c> by its shape rather than a resolved symbol, shared by the
/// rules that suggest a span in place of a copy (PSH1013, PSH1212, PSH1217, PSH1222). A same-named type in
/// another namespace never matches.
/// </summary>
internal static class ReadOnlySpanType
{
    /// <summary>The simple name of the span type.</summary>
    private const string ReadOnlySpanTypeName = "ReadOnlySpan";

    /// <summary>Returns whether a type is <c>System.ReadOnlySpan&lt;T&gt;</c>, and its element type.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="elementType">The span's element type, when the type is the span.</param>
    /// <returns><see langword="true"/> for a constructed <c>System.ReadOnlySpan&lt;T&gt;</c>.</returns>
    internal static bool TryGetElementType(ITypeSymbol? type, [NotNullWhen(true)] out ITypeSymbol? elementType)
    {
        if (type is INamedTypeSymbol
            {
                Name: ReadOnlySpanTypeName,
                IsGenericType: true,
                TypeArguments.Length: 1,
                ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
            } span)
        {
            elementType = span.TypeArguments[0];
            return true;
        }

        elementType = null;
        return false;
    }

    /// <summary>Returns whether a type is <c>System.ReadOnlySpan&lt;T&gt;</c> over a given special element type.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="elementType">The special type the span must hold.</param>
    /// <returns><see langword="true"/> for, say, <c>ReadOnlySpan&lt;char&gt;</c> when <paramref name="elementType"/> is <see cref="SpecialType.System_Char"/>.</returns>
    internal static bool IsSpanOf(ITypeSymbol? type, SpecialType elementType) =>
        TryGetElementType(type, out var element) && element.SpecialType == elementType;
}
