// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2452 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2452 — a method marked pure has no result a caller could observe.</summary>
    public static readonly DiagnosticDescriptor PureMethodWithoutResult = Create(
        "SST2452",
        "A pure method should return a value",
        "'{0}' is marked [Pure] but returns {1}; a pure method's only effect is its result, so the attribute is either wrong or the method does nothing",
        PureMethodWithoutResultDescription);

    /// <summary>The PureMethodWithoutResult rule description.</summary>
    private const string PureMethodWithoutResultDescription =
        "Marking a method [Pure] promises it changes no visible state, so calling it is only useful for the value it returns. A void method "
        + "has no value to return: either the method does have side effects and the attribute misleads every reader and tool that trusts it, "
        + "or it truly has none and every call is dead code. A bare Task or ValueTask is the same contradiction one step removed — the "
        + "completion of a computation that changed nothing and produced nothing cannot be observed either. Return the computed value, or "
        + "remove the attribute.";
}
