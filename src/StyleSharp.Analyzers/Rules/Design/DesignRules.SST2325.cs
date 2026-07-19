// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2325 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2325 — an async method's argument check is stranded after its first await.</summary>
    public static readonly DiagnosticDescriptor AsyncValidatesAfterAwait = Create(
        "SST2325",
        "Validate an async method's arguments before its first await",
        "This argument check in '{0}' runs after the first await, so it does not fail until the returned task is awaited",
        AsyncValidatesAfterAwaitDescription);

    /// <summary>The AsyncValidatesAfterAwait rule description.</summary>
    private const string AsyncValidatesAfterAwaitDescription =
        "An async method starts running when it is called, but only up to its first await; everything past that runs on a continuation "
        + "once the returned task is awaited. An argument guard placed after the first await therefore does not throw at the call site, "
        + "where the caller's stack still says who passed the bad value, but later, when the task is awaited. Validate the arguments before "
        + "the first await, or split a synchronous validating method from a private async implementation.";
}
