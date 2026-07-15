// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The PSH1316 descriptor.</summary>
internal static partial class ConcurrencyRules
{
    /// <summary>PSH1316 — a <c>ValueTask</c> local is consumed a second time, through a loop or a copy.</summary>
    public static readonly DiagnosticDescriptor ConsumeValueTaskOnce = Create(
        "PSH1316",
        "Consume a ValueTask exactly once",
        "'{0}' consumes a ValueTask whose pooled token the first consume already spent, so this reads a recycled result",
        ConsumeValueTaskOnceDescription);

    /// <summary>The ConsumeValueTaskOnce rule description.</summary>
    private const string ConsumeValueTaskOnceDescription =
        "A ValueTask is a single-use handle to a pooled IValueTaskSource token, and the first consume — an await, '.Result', "
        + "'.GetAwaiter()', or '.AsTask()' — invalidates that token and returns it to the pool. Consuming the same instance a "
        + "second time reads a recycled token and silently returns another caller's result. This reports the two shapes that "
        + "read one instance more than once: a ValueTask local declared outside a loop and awaited inside it, so every iteration "
        + "after the first reuses a spent token; and a local copied into a second local where both are consumed. Hoist the "
        + "producing call into the loop so each iteration gets a fresh ValueTask, or call '.Preserve()' once when the value must "
        + "genuinely be read again.";
}
