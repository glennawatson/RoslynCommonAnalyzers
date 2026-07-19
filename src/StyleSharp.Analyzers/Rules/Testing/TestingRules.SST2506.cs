// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2506 descriptor.</summary>
internal static partial class TestingRules
{
    /// <summary>SST2506 — a test method pauses on a fixed real-time delay via Thread.Sleep.</summary>
    public static readonly DiagnosticDescriptor ThreadSleepInTest = Create(
        "SST2506",
        "A test should not pause on a fixed real-time delay",
        "This test calls 'Thread.Sleep', which blocks for a fixed real-time delay and races the wall clock; wait for the condition the test needs instead",
        ThreadSleepInTestDescription);

    /// <summary>The ThreadSleepInTest rule description.</summary>
    private const string ThreadSleepInTestDescription =
        "A test that calls 'Thread.Sleep' pauses the thread for a fixed real-time delay. That delay is spent on every run, so a "
        + "suite full of these sleeps grows slower with every test that adds one, whether or not the awaited work is already done. "
        + "Worse, the delay is a guess: it is either longer than the work needs — wasted time — or, on a loaded or slow machine, "
        + "shorter than the work needs, at which point the test observes a half-finished state and fails intermittently. A sleep is "
        + "the classic source of a flaky test, because it races a wall clock instead of waiting for the thing the test actually "
        + "depends on. Wait for that condition directly — poll it, await the task or signal that completes it, or advance a fake "
        + "clock the code under test reads — so the test proceeds the instant the work is done and never a moment before. The rule "
        + "reports only a call that binds to 'System.Threading.Thread.Sleep' inside a method marked as a test, so a same-named "
        + "method of your own and a sleep in production code are both left alone.";
}
