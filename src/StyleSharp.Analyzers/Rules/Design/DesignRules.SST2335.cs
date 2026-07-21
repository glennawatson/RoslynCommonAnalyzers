// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2335 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2335 — parts of a partial type disagree on the 'static' modifier; opt-in, disabled by default.</summary>
    public static readonly DiagnosticDescriptor PartialTypeStaticModifierMismatch = CreateDisabled(
        "SST2335",
        "Declare the same 'static' modifier on every part of a partial type",
        "This part of '{0}' omits 'static' while another part declares it; add 'static' here",
        PartialTypeStaticModifierMismatchDescription);

    /// <summary>The PartialTypeStaticModifierMismatch rule description.</summary>
    private const string PartialTypeStaticModifierMismatchDescription =
        "When one part of a partial class is declared 'static' and another leaves it off, the type still compiles as static — the "
        + "one part that names 'static' decides it — but a reader looking at the part without the keyword sees an ordinary class "
        + "and can waste time trying to add instance state or a constructor to it. Repeating 'static' on every part makes each one "
        + "tell the truth on its own. This is a house-style consistency nudge, not a defect, so it is off by default and opt-in "
        + "through '.editorconfig'.";
}
