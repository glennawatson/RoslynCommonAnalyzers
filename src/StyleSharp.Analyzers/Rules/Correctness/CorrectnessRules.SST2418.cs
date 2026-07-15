// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2418 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2418 — the result of an immutable value's method is discarded.</summary>
    public static readonly DiagnosticDescriptor DiscardedImmutableResult = Create(
        "SST2418",
        "The result of an immutable value's method should be used",
        "'{0}' is immutable, so '{1}' returns a new value and this call discards it",
        DiscardedImmutableResultDescription);

    /// <summary>The DiscardedImmutableResult rule description.</summary>
    private const string DiscardedImmutableResultDescription =
        "A method is called on an immutable value as a whole statement, and its return value is thrown away. Because the receiver "
        + "cannot mutate, the method's only effect is the value it returns — 'date.AddDays(1);' computes a new date and drops it, "
        + "leaving 'date' unchanged. The mistake comes from expecting the call to modify the receiver in place, as it would on a "
        + "mutable object. The rule recognises the immutable receiver from its type — a readonly struct, a span or memory slice, or a "
        + "well-known immutable value type — rather than a fixed list of methods, so it covers user-defined readonly record structs "
        + "too. Shapes that a discarded-object or discarded-string diagnostic already reports are left alone.";
}
