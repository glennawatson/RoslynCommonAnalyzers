// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2491 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2491 — a non-async method returns a pending task from inside a scope that tears down at return.</summary>
    public static readonly DiagnosticDescriptor AwaitableReturnedFromTeardown = Create(
        "SST2491",
        "Do not return a pending task from inside a using, lock, or try/finally",
        "This '{0}' releases its resource the moment the method returns, before the returned task completes; make the method 'async' and 'await' the call so teardown runs after the work finishes",
        AwaitableReturnedFromTeardownDescription);

    /// <summary>The AwaitableReturnedFromTeardown rule description.</summary>
    private const string AwaitableReturnedFromTeardownDescription =
        "A method that is not 'async' returns a task directly while a using statement, a using declaration, a lock, or a "
        + "try/finally still governs a resource. Returning the task hands control back to the caller at once, so the "
        + "resource is disposed, the lock is released, or the finally runs immediately — before the returned task has "
        + "completed. The asynchronous work then runs against a disposed object or without the lock it was written to hold, "
        + "a use-after-teardown bug that compiles and usually passes a quick test because the race only shows under load. "
        + "Making the method 'async' and awaiting the call keeps the method on the stack until the work finishes, so the "
        + "teardown happens after completion, where it was meant to.";
}
