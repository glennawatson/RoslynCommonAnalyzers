// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2499 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2499 — reading ExitCode after WaitForExit cannot tell a normal exit from a signal.</summary>
    public static readonly DiagnosticDescriptor ProcessExitStatusIgnoresSignal = Create(
        "SST2499",
        "Read the exit status rather than the exit code when a process can be signalled",
        "'ExitCode' cannot distinguish a normal exit from termination by a signal; use 'WaitForExitStatus' instead",
        ProcessExitStatusIgnoresSignalDescription);

    /// <summary>The ProcessExitStatusIgnoresSignal rule description.</summary>
    private const string ProcessExitStatusIgnoresSignalDescription =
        "On Unix a process that is killed by a signal has no exit code of its own. 'ExitCode' reports a value derived from "
        + "the signal number instead, in the same range a process can return deliberately, so code that branches on it "
        + "cannot tell 'the child chose to fail with 137' from 'the kernel killed the child with SIGKILL'. That difference "
        + "usually decides whether to retry, so conflating them turns an out-of-memory kill into an ordinary failure and "
        + "hides the cause. 'WaitForExitStatus' returns a status that states which of the two happened and carries the "
        + "signal when a signal ended the process. Reported only where 'ExitCode' is read in the same member as a "
        + "'WaitForExit' call -- the shape that is waiting on a child and then deciding what its result means -- and only "
        + "when the referenced framework actually offers 'WaitForExitStatus', so the suggestion is always actionable.";
}
