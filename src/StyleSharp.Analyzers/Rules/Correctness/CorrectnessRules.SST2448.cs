// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2448 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2448 — the outcome of a delegate subtraction depends on combination order.</summary>
    public static readonly DiagnosticDescriptor DelegateSubtraction = Create(
        "SST2448",
        "Delegate subtraction has order-dependent results",
        "'{0}' is removed by delegate subtraction, which strips its handlers only when they sit as one contiguous run in the target — the order the handlers were combined in decides the result",
        DelegateSubtractionDescription);

    /// <summary>The DelegateSubtraction rule description.</summary>
    private const string DelegateSubtractionDescription =
        "Delegate subtraction removes the right-hand invocation list from the left-hand one only when that list appears in it as one "
        + "contiguous run, and when it appears more than once only the last run is removed. Subtracting a combined delegate therefore "
        + "succeeds or silently leaves every handler in place depending on the order the handlers were added — a fact the call site "
        + "cannot see. Remove handlers one at a time, or track them in a collection instead of a combined delegate.";
}
