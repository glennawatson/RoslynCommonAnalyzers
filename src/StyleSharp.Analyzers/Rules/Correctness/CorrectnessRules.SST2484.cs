// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2484 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2484 — a raw handle is read through a safe handle's dangerous accessor.</summary>
    public static readonly DiagnosticDescriptor DangerousGetHandle = Create(
        "SST2484",
        "A raw handle read through DangerousGetHandle is not reference-counted",
        "'DangerousGetHandle' returns the raw handle without holding a reference count, so a concurrent dispose or finalize can recycle it and the value is used after free",
        DangerousGetHandleDescription);

    /// <summary>The DangerousGetHandle rule description.</summary>
    private const string DangerousGetHandleDescription =
        "'SafeHandle.DangerousGetHandle' hands back the wrapped 'IntPtr' without taking a reference on the handle. The moment it "
        + "returns, nothing keeps the handle alive: if another thread disposes the safe handle, or the finalizer runs because the "
        + "safe handle is no longer reachable, the operating system frees the underlying handle and can hand the same numeric value "
        + "to an unrelated resource. Code still holding the raw value then reads or writes a freed handle — a crash — or, worse, a "
        + "live handle that now belongs to a different object, silently corrupting it. Take a counted reference around the raw value "
        + "with 'DangerousAddRef'/'DangerousRelease' (paired in a 'try'/'finally'), or avoid the raw handle entirely and pass the "
        + "safe handle itself to the API that consumes it.";
}
