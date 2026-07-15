// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reads a single unambiguous symbol out of a <see cref="SymbolInfo"/>.</summary>
internal static class SymbolResolution
{
    /// <summary>Returns the resolved symbol when the semantic info names exactly one.</summary>
    /// <param name="symbolInfo">The symbol information.</param>
    /// <returns>The bound symbol, the sole candidate when binding was ambiguous with one candidate, or <see langword="null"/>.</returns>
    public static ISymbol? GetSingleSymbol(SymbolInfo symbolInfo)
        => symbolInfo.Symbol ?? (symbolInfo.CandidateSymbols.Length == 1 ? symbolInfo.CandidateSymbols[0] : null);
}
