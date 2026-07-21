// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2330 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2330 — a flags enum member is a numeric literal that equals a combination of other members.</summary>
    public static readonly DiagnosticDescriptor FlagsCombinationLiteralShouldNameMembers = CreateInfo(
        "SST2330",
        "Write a combined flags value as the members it combines",
        "'{0}' is a numeric literal equal to combining other flags; write it as '{1}'",
        FlagsCombinationLiteralShouldNameMembersDescription);

    /// <summary>The FlagsCombinationLiteralShouldNameMembers rule description.</summary>
    private const string FlagsCombinationLiteralShouldNameMembersDescription =
        "On a flags enum, a member assigned a bare number that happens to equal several single-bit members OR'd together — "
        + "'All = 7' where 'Read = 1', 'Write = 2', 'Execute = 4' — hides its own meaning. The reader has to add the bits back up "
        + "to see what it is, and the moment one of the underlying members changes value the literal is silently wrong while still "
        + "compiling. Writing it as 'Read | Write | Execute' says what the value is, and stays correct when the members it names "
        + "are renumbered. Reported only when the literal decomposes exactly into two or more of the enum's own single-bit members.";
}
