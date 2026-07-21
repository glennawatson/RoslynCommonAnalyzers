// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2496 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2496 — an explicit dispose repeats disposal a governing <c>using</c> already performs.</summary>
    public static readonly DiagnosticDescriptor RedundantDispose = CreateInfo(
        "SST2496",
        "Do not dispose a value an enclosing using already disposes",
        "'{0}' is already disposed by its enclosing 'using'; this explicit '{1}' call disposes it a second time",
        RedundantDisposeDescription);

    /// <summary>The RedundantDispose rule description.</summary>
    private const string RedundantDisposeDescription =
        "A local governed by a using statement or a using declaration is disposed again by an explicit Dispose() or "
        + "Close() call inside the same scope. The using already disposes the value when its scope ends, so the explicit "
        + "call runs a second disposal. Many types tolerate a repeat disposal, but the contract does not require it, and "
        + "the double call reads as though one of the two owners is responsible when the using already is. Removing the "
        + "explicit call leaves disposal to the using, where the ownership is clear and runs exactly once.";
}
