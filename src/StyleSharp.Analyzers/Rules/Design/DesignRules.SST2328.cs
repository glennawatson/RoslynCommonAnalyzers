// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2328 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2328 — a visible field or property hands a raw native pointer out across the type boundary.</summary>
    public static readonly DiagnosticDescriptor ExposedNativePointer = Create(
        "SST2328",
        "Do not expose a raw native pointer handle",
        "'{0}' exposes a raw native pointer; a caller can read, write, free, or corrupt the native memory it points at — keep the value private and hand out a SafeHandle",
        ExposedNativePointerDescription);

    /// <summary>The ExposedNativePointer rule description.</summary>
    private const string ExposedNativePointerDescription =
        "A visible instance field or property whose type is 'IntPtr', 'UIntPtr', 'nint', or 'nuint' hands the raw value of a native "
        + "handle out across the type boundary. Whoever the modifier admits — 'public', 'protected', or 'protected internal' reaches "
        + "outside the assembly — can then read the pointer, write a rogue one, or pass it to a native free, reaching straight past "
        + "the type that owns the memory and corrupting or double-freeing it. Keep the raw value in a 'private' field and wrap the "
        + "resource in a 'SafeHandle': its critical finalization and ref-counted release keep the handle valid for exactly as long "
        + "as it is in use, and callers hold the wrapper rather than the bare address. Turning the field into a visible property does "
        + "not help — the property is reported the same way, because it exposes the same value. A 'private' or 'internal' member keeps "
        + "the handle under the assembly's own control and is left alone, as is a 'static' one, which is not part of an instance's surface.";
}
