// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2488 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2488 — a catch whose only effect is to log the caught exception and rethrow it.</summary>
    public static readonly DiagnosticDescriptor LogAndRethrow = Create(
        "SST2488",
        "Do not log and rethrow the same exception",
        "This catch logs the caught exception and then rethrows it, so the same failure is recorded here and again where it is finally handled",
        LogAndRethrowDescription);

    /// <summary>The LogAndRethrow rule description.</summary>
    private const string LogAndRethrowDescription =
        "A catch whose body is a logging call followed by a bare 'throw;' handles the same exception twice. It is logged here, then re-raised "
        + "unchanged to be logged again by whatever finally handles it, so one failure produces two or more entries and the stack fills with "
        + "duplicate, misleading noise. Decide which handler owns the exception: either handle it here — log it and recover with a fallback "
        + "instead of rethrowing — or remove the logging and let it propagate to a single outer handler. Logging plus 'throw;' is doing both.";
}
