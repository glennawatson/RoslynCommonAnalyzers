// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2436 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2436 — an event is raised with a null sender or null event args.</summary>
    public static readonly DiagnosticDescriptor NullEventRaise = Create(
        "SST2436",
        "Do not raise an event with a null sender or null args",
        "raising '{0}' with a null {1} throws in every subscriber that reads it; pass '{2}'",
        NullEventRaiseDescription);

    /// <summary>The NullEventRaise rule description.</summary>
    private const string NullEventRaiseDescription =
        "Raising an instance event as 'Changed?.Invoke(null, e)' or 'Changed?.Invoke(this, null)' hands every subscriber a "
        + "null sender or a null event-args object. A handler that reads either throws a NullReferenceException, and the "
        + "stack trace points at the subscriber rather than the code that raised the event, so the cause is hard to find. "
        + "Pass 'this' as the sender of an instance event and 'EventArgs.Empty' (or a real event-args instance) as the args. "
        + "A static event is left alone, because a null sender is correct when there is no instance to name.";
}
