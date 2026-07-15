// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2414 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2414 — two branches of one conditional share an implementation.</summary>
    public static readonly DiagnosticDescriptor DuplicateBranchImplementation = Create(
        "SST2414",
        "Two branches should not share an implementation",
        "This branch has the same body as another; one of them was probably meant to differ",
        DuplicateBranchImplementationDescription);

    /// <summary>The DuplicateBranchImplementation rule description.</summary>
    private const string DuplicateBranchImplementationDescription =
        "Two arms of one 'if'/'else if' chain, or two sections of one 'switch', run a token-identical body while other arms differ. "
        + "The two conditions are distinguished at the top and then handled the same way, which usually means a copy-and-paste left one "
        + "arm holding the value that belonged to the other. Where the duplication is intended, the two cases belong together: a "
        + "'switch' merges them into one section with an 'or' pattern, and side-effect-free 'if' conditions merge with '||'.";
}
