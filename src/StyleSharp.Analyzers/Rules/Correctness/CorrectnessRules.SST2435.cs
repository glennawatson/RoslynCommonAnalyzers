// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2435 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2435 — a base class's value-equality <c>Equals</c> is used as an early-out fast path.</summary>
    public static readonly DiagnosticDescriptor ValueEqualityUsedAsFastPath = Create(
        "SST2435",
        "Do not use a base value equality as an equality fast path",
        "base.Equals binds to '{0}', which compares by value, so returning early when it is true skips this type's own fields; combine it with '&&' instead",
        ValueEqualityUsedAsFastPathDescription);

    /// <summary>The ValueEqualityUsedAsFastPath rule description.</summary>
    private const string ValueEqualityUsedAsFastPathDescription =
        "The reference-equality shortcut 'if (ReferenceEquals-like check) return true;' is only valid when the base call really is reference "
        + "equality - that is, when the base is object. In a hierarchy whose base class overrides Equals with value semantics, base.Equals "
        + "returns true whenever the base fields match, so an early return true (or a base.Equals(...) || ... short-circuit) reports two "
        + "instances as equal even when this type's own fields differ. The derived comparison never runs. The correct shape combines the base "
        + "and derived comparisons with '&&': base.Equals(obj) && <this type's fields match>.";
}
