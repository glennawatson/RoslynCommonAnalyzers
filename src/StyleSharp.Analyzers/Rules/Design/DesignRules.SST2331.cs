// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2331 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2331 — an enum leaves member values implicit; opt-in, disabled by default.</summary>
    public static readonly DiagnosticDescriptor EnumMembersShouldBeExplicit = CreateDisabled(
        "SST2331",
        "Give enum members explicit values",
        "'{0}' has members whose values are left to declaration order; assign each an explicit value",
        EnumMembersShouldBeExplicitDescription);

    /// <summary>The EnumMembersShouldBeExplicit rule description.</summary>
    private const string EnumMembersShouldBeExplicitDescription =
        "When an enum member has no '= value', its number is the position it happens to sit in. That number is real: it is what "
        + "gets persisted, sent on the wire, and compared against — and it moves the moment someone inserts, removes, or reorders "
        + "a member above it, silently repointing stored data at the wrong name. Assigning every member an explicit value pins the "
        + "mapping so a later edit to the list cannot shift it. This is an opinionated, house-style rule — many enums are never "
        + "persisted and read fine as an ordered list — so it is off by default and opt-in through '.editorconfig'.";
}
