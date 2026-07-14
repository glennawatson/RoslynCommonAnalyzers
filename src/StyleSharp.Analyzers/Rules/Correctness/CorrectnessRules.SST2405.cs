// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2405 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2405 — a debugger display string names a member that does not exist.</summary>
    public static readonly DiagnosticDescriptor DebuggerDisplayNamesMissingMember = Create(
        "SST2405",
        "DebuggerDisplay should reference members that exist",
        "'{0}' names '{1}', which '{2}' does not declare",
        DebuggerDisplayNamesMissingMemberDescription);

    /// <summary>The DebuggerDisplayNamesMissingMember rule description.</summary>
    private const string DebuggerDisplayNamesMissingMemberDescription =
        "The expression in a [DebuggerDisplay] is resolved by the debugger, not the compiler, so a typo or a renamed member survives the "
        + "build and shows up as an error string in the watch window — at exactly the moment someone is trying to debug something else.";
}
