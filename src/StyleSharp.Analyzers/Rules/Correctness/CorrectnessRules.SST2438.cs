// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2438 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2438 — an error log in a catch throws the caught exception away.</summary>
    public static readonly DiagnosticDescriptor ExceptionDiscardedInCatch = Create(
        "SST2438",
        "A caught exception should be passed to the logger",
        "This log discards '{0}'; pass it as the exception argument so the stack trace reaches the sink",
        ExceptionDiscardedInCatchDescription);

    /// <summary>The ExceptionDiscardedInCatch rule description.</summary>
    private const string ExceptionDiscardedInCatchDescription =
        "A catch that logs at error or critical level but never hands the caught exception to the logger loses everything a structured "
        + "sink needs: the type, the stack trace, the inner exceptions. Every logging method has an overload that takes the exception as a "
        + "dedicated argument, and only that argument is projected into the exception fields a sink reads. Passing a hand-picked property "
        + "such as the message text instead records a degraded copy and throws the rest away. Reported only when the exception is provably "
        + "discarded: a catch with a named exception variable, a log at error or critical level, no rethrow in the block, and the exception "
        + "either used only for one of its properties or not passed at all.";
}
