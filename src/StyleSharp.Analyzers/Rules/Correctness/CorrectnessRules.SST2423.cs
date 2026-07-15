// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2423 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2423 — a disposable created in a <c>using</c> is returned out of that scope.</summary>
    public static readonly DiagnosticDescriptor DisposableReturnedFromUsing = Create(
        "SST2423",
        "Do not return a disposable owned by a using",
        "'{0}' is disposed when this scope ends, so the caller receives an already-disposed object",
        DisposableReturnedFromUsingDescription);

    /// <summary>The DisposableReturnedFromUsing rule description.</summary>
    private const string DisposableReturnedFromUsingDescription =
        "A 'using' disposes its value the moment control leaves the scope — including on the way out through a 'return'. "
        + "Returning that value hands the caller an object that was disposed during the return, and the failure surfaces later, "
        + "somewhere else, as an ObjectDisposedException with a stack that points nowhere near the cause. The fix is to transfer "
        + "ownership: drop the 'using' and let the caller dispose the value, which is the opposite of the instinct to wrap it in "
        + "a try/finally. When the method is async and the value is IAsyncDisposable, the caller then needs 'await using'.";
}
