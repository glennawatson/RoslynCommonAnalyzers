// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST1222 descriptor.</summary>
internal static partial class OrderingRules
{
    /// <summary>SST1222 — an enum whose members all carry explicit values declares them out of order.</summary>
    public static readonly DiagnosticDescriptor EnumMemberOrder = CreateInfo(
        "SST1222",
        "Enum members should be declared in ascending value order",
        "Enum member '{0}' is out of order: declare members in ascending value order",
        EnumMemberOrderDescription);

    /// <summary>The EnumMemberOrder rule description.</summary>
    private const string EnumMemberOrderDescription =
        "When every member of an enum names its own number, the declaration order is free — and the only order "
        + "a reader can check against is ascending, because that is the one that makes a gap or a repeat "
        + "visible at a glance. Reported only when every member has an explicit constant value, so the rule "
        + "never asks for a reordering that would change what the implicit numbering assigns. A '[Flags]' enum "
        + "is left alone: there the grouping of single bits and the combinations built from them carries more "
        + "than the numeric order does.";
}
