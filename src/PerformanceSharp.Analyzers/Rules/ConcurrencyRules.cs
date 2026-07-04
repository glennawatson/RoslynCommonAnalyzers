// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the concurrency and async performance rules (PSH13xx).
/// These target cheaper synchronization and task patterns, and are gated on the
/// relevant runtime type existing so they light up only where the replacement
/// compiles.
/// </summary>
internal static class ConcurrencyRules
{
    /// <summary>PSH1300 — a dedicated <c>object</c> lock field should be a <c>System.Threading.Lock</c>.</summary>
    public static readonly DiagnosticDescriptor PreferLockType = Create(
        "PSH1300",
        "Use System.Threading.Lock for a dedicated lock object",
        "Change the type of '{0}' to System.Threading.Lock",
        "A private readonly object used only as a lock target is declared as System.Threading.Lock (.NET 9+), which the compiler locks through a typed scope rather than Monitor.");

    /// <summary>PSH1301 — a single task does not need a WhenAll or WaitAll wrapper.</summary>
    public static readonly DiagnosticDescriptor AwaitSingleTaskDirectly = Create(
        "PSH1301",
        "Do not wrap a single task in WhenAll or WaitAll",
        "Use the task directly instead of '{0}'",
        "Task.WhenAll and Task.WaitAll with one task allocate an array and combining machinery to coordinate nothing; awaiting or waiting the task directly skips both.");

    /// <summary>PSH1302 — a TaskCompletionSource should opt into asynchronous continuations.</summary>
    public static readonly DiagnosticDescriptor RunContinuationsAsynchronously = Create(
        "PSH1302",
        "TaskCompletionSource should run continuations asynchronously",
        "Pass TaskCreationOptions.RunContinuationsAsynchronously to this TaskCompletionSource",
        RunContinuationsAsynchronouslyDescription);

    /// <summary>PSH1303 — an async method should await a delay rather than block the thread.</summary>
    public static readonly DiagnosticDescriptor NoThreadSleepInAsync = Create(
        "PSH1303",
        "Do not block an async method with Thread.Sleep",
        "Replace this Thread.Sleep call with 'await Task.Delay'",
        NoThreadSleepInAsyncDescription);

    /// <summary>PSH1304 — a delay-paced polling loop should use a PeriodicTimer.</summary>
    public static readonly DiagnosticDescriptor UsePeriodicTimer = Create(
        "PSH1304",
        "Use PeriodicTimer for periodic asynchronous work",
        "Use a PeriodicTimer instead of pacing this loop with Task.Delay",
        UsePeriodicTimerDescription);

    /// <summary>PSH1305 — enumerate a concurrent dictionary directly, not its Keys/Values snapshots.</summary>
    public static readonly DiagnosticDescriptor NoConcurrentSnapshotEnumeration = Create(
        "PSH1305",
        "Enumerate a ConcurrentDictionary directly instead of a Keys or Values snapshot",
        "Enumerate the dictionary's key/value pairs instead of the '{0}' snapshot",
        NoConcurrentSnapshotEnumerationDescription);

    /// <summary>PSH1306 — a bool once-guard read-then-set races; an interlocked latch does not. Opt-in.</summary>
    public static readonly DiagnosticDescriptor InterlockedOnceGuard = CreateOptIn(
        "PSH1306",
        "Guard one-time execution with an interlocked latch",
        "This check-then-set on '{0}' can let two threads through; use Interlocked.Exchange on an int latch",
        InterlockedOnceGuardDescription);

    /// <summary>PSH1307 — a field that is an Interlocked target elsewhere should not be accessed plainly.</summary>
    public static readonly DiagnosticDescriptor VolatileInterlockedField = Create(
        "PSH1307",
        "Access interlocked fields with Volatile",
        "'{0}' is an Interlocked target elsewhere in this type; use '{1}' for this access",
        VolatileInterlockedFieldDescription);

    /// <summary>PSH1308 — a Task.FromResult whose value is never observed should be Task.CompletedTask.</summary>
    public static readonly DiagnosticDescriptor UseCompletedTask = Create(
        "PSH1308",
        "Return the completed task instead of Task.FromResult",
        "Use Task.CompletedTask; this result is only ever observed as a plain Task",
        UseCompletedTaskDescription);

    /// <summary>PSH1309 — cancellation callbacks should skip the execution-context capture. Opt-in.</summary>
    public static readonly DiagnosticDescriptor UseUnsafeRegister = CreateOptIn(
        "PSH1309",
        "Register cancellation callbacks without flowing the execution context",
        "Use UnsafeRegister instead of Register for this cancellation callback",
        UseUnsafeRegisterDescription);

    /// <summary>The PSH1302 rule description.</summary>
    private const string RunContinuationsAsynchronouslyDescription =
        "Completing a TaskCompletionSource without TaskCreationOptions.RunContinuationsAsynchronously runs every waiter's continuation inline "
        + "on the completing thread, which can stall that thread for an unpredictable time or deadlock when the completer holds a lock; "
        + "the flag queues continuations to the scheduler instead.";

    /// <summary>The PSH1303 rule description.</summary>
    private const string NoThreadSleepInAsyncDescription =
        "Thread.Sleep inside an async method holds a thread-pool thread hostage for the whole pause, defeating the scalability the async "
        + "signature promises; awaiting Task.Delay frees the thread and resumes the method when the time elapses.";

    /// <summary>The PSH1304 rule description.</summary>
    private const string UsePeriodicTimerDescription =
        "A loop paced by 'await Task.Delay' allocates a fresh timer and task on every iteration and drifts by the loop body's execution time; "
        + "PeriodicTimer (.NET 6+) reuses one timer, ticks on a fixed cadence, and stops cleanly through its dispose or a cancellation token.";

    /// <summary>The PSH1305 rule description.</summary>
    private const string NoConcurrentSnapshotEnumerationDescription =
        "ConcurrentDictionary's Keys and Values properties lock every bucket and copy the whole collection into a new list on each access; "
        + "enumerating the dictionary itself is lock-free and allocates only an enumerator.";

    /// <summary>The PSH1306 rule description.</summary>
    private const string InterlockedOnceGuardDescription =
        "A bool once-guard written as a read, an early return, and a later assignment lets every thread that passes the check before the "
        + "first write run the protected code; Interlocked.Exchange on an int field admits exactly one caller for one atomic instruction. "
        + "Whether a guard must be thread-safe is contextual, so the rule is opt-in.";

    /// <summary>The PSH1307 rule description.</summary>
    private const string VolatileInterlockedFieldDescription =
        "A field updated through Interlocked participates in lock-free synchronization, but the compiler and CPU may reorder or cache its "
        + "plain reads and writes, hiding updates from other threads; Volatile.Read and Volatile.Write pair correctly with interlocked "
        + "updates. Construction-time accesses are not reported.";

    /// <summary>The PSH1308 rule description.</summary>
    private const string UseCompletedTaskDescription =
        "Task.FromResult allocates a Task<TResult> to carry a value, and a caller that only sees the non-generic Task can never read it; "
        + "Task.CompletedTask returns the shared cached instance. Suggested only where the API exists.";

    /// <summary>The PSH1309 rule description.</summary>
    private const string UseUnsafeRegisterDescription =
        "CancellationToken.Register captures the current ExecutionContext and restores it inside the callback on every registration; "
        + "UnsafeRegister skips the capture. Callbacks that read AsyncLocal state would observe different values, so the rule is opt-in. "
        + "Suggested only where the API exists.";

    /// <summary>Creates a Warning-severity Concurrency descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Concurrency", description);

    /// <summary>Creates a Concurrency descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.CreateOptIn(id, title, messageFormat, "Concurrency", description);
}
