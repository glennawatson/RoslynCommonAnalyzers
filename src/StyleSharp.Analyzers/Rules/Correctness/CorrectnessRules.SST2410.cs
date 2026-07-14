// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2410 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2410 — a disposable is created into a local and never disposed.</summary>
    public static readonly DiagnosticDescriptor DisposableNeverDisposed = Create(
        "SST2410",
        "A created disposable should be disposed",
        "'{0}' is never disposed",
        DisposableNeverDisposedDescription);

    /// <summary>The DisposableNeverDisposed rule description.</summary>
    private const string DisposableNeverDisposedDescription =
        "The local holds the only reference to something the method built and owns — a handle, a socket, a timer — and the method "
        + "returns without releasing it. Nothing fails at the point of the leak; the cost arrives later, as a file that stays locked or "
        + "a connection that is never given back. This is an ownership check, not a dataflow one: it reports only a local that is "
        + "created with 'new', used where it stands, and dropped. The moment the value is handed anywhere else — returned, stored, "
        + "passed to a method or a constructor, added to a collection, captured — the rule says nothing, because whoever received it "
        + "may be the one that disposes it.";
}
