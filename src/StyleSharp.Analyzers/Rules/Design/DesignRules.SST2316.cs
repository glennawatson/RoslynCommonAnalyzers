// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2316 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2316 — a type has a <c>Dispose</c>/<c>DisposeAsync</c> method but does not implement the matching interface.</summary>
    public static readonly DiagnosticDescriptor DisposeWithoutInterface = Create(
        "SST2316",
        "A disposal method needs its disposal interface",
        "'{0}' declares '{1}' but does not implement '{2}', so callers that dispose through the interface never call it",
        DisposeWithoutInterfaceDescription);

    /// <summary>The DisposeWithoutInterface rule description.</summary>
    private const string DisposeWithoutInterfaceDescription =
        "A public 'Dispose()' or 'DisposeAsync()' on a type that does not implement the matching interface is cleanup nobody "
        + "runs. DI containers, service scopes, and composite owners all dispose through 'IDisposable'/'IAsyncDisposable'; a type "
        + "that has the method but not the interface is registered, held for the life of the process, and never cleaned up — "
        + "silently, because the code compiles and looks correct. Implement the interface the method already matches. A "
        + "'ref struct' is exempt: it cannot implement an interface on older language versions and its 'using' binds to the "
        + "method by pattern, so a bare 'Dispose()' there is correct. A duck-typed enumerator, an explicit interface "
        + "implementation, and a method inherited from a base that already implements the interface are exempt for the same "
        + "reason — the disposal is already wired up.";
}
