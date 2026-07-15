// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2317 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2317 — a disposable owns a raw native handle with no finalizer to close it.</summary>
    public static readonly DiagnosticDescriptor NativeResourceWithoutSafeHandle = Create(
        "SST2317",
        "Wrap an owned native handle in a SafeHandle",
        "'{0}' owns the native resource '{1}' with no finalizer; wrap it in a SafeHandle so it is released even when Dispose is not called",
        NativeResourceWithoutSafeHandleDescription);

    /// <summary>The NativeResourceWithoutSafeHandle rule description.</summary>
    private const string NativeResourceWithoutSafeHandleDescription =
        "A disposable that holds an owned native resource in a raw IntPtr field, and releases it only on the disposal path, "
        + "leaks that resource whenever Dispose is not called — an exception before disposal, a caller that forgets, a finalizer "
        + "the type does not have. Wrap the handle in a SafeHandle: it is a critical-finalization object with ref-counted "
        + "release and guaranteed run order, and it closes the use-after-free window a hand-rolled finalizer leaves open. "
        + "SafeFileHandle, SafeWaitHandle, or a small SafeHandleZeroOrMinusOneIsInvalid subclass are the on-ramps; where the "
        + "field is raw memory rather than an OS handle, NativeMemory.Alloc/Free (.NET 6+) is the fit. The rule reports only the "
        + "no-finalizer case, where the leak is real, and only when the field is passed to a call on the disposal path — proof "
        + "that it is an owned resource rather than an opaque cookie. A field already wrapped in a SafeHandle is left alone.";
}
