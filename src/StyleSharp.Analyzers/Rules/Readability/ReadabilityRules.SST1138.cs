// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST1138 descriptor.</summary>
internal static partial class ReadabilityRules
{
    /// <summary>SST1138 — a free-standing block declares nothing and only nests its statements.</summary>
    public static readonly DiagnosticDescriptor FreeStandingBlock = Create(
        "SST1138",
        "A free-standing block that declares nothing should be removed",
        "This block scopes nothing; splice its statements into the enclosing block",
        FreeStandingBlockDescription);

    /// <summary>The FreeStandingBlock rule description.</summary>
    private const string FreeStandingBlockDescription =
        "A brace-delimited block appears directly inside another block and declares nothing — no local, no local function, no label. "
        + "Because it introduces no scope, it is identical to splicing its statements into the parent, and it is usually the leftover "
        + "of a deleted 'if', 'using', 'lock', or 'fixed' whose statements were kept. A block that declares a local is real scoping "
        + "and is left alone.";
}
