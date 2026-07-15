// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2427 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2427 — a derived overload is general enough to hide a more specific base overload.</summary>
    public static readonly DiagnosticDescriptor HidingGeneralOverload = Create(
        "SST2427",
        "A derived overload should not hide a base overload with a more specific parameter",
        "'{0}' accepts '{1}', a base type of the '{2}' that '{3}' takes, so calls through this type never reach the base overload",
        HidingGeneralOverloadDescription);

    /// <summary>The HidingGeneralOverload rule description.</summary>
    private const string HidingGeneralOverloadDescription =
        "Overload resolution stops at the first type in the hierarchy that has an applicable member. When a derived class "
        + "declares an overload whose parameter is a base type of a same-named base-class overload's parameter, the derived "
        + "overload is applicable everywhere the base one is — so a call through the derived type binds to it and never reaches "
        + "the base overload. A caller passing the specific type quietly gets the general handler, which usually means a value "
        + "boxed or widened and the specialised code skipped. Declaring the specific overload as well, renaming it, or making it "
        + "an 'override' resolves the ambiguity in favour of the caller's intent.";
}
