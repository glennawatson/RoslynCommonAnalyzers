// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The PSH1317 descriptor.</summary>
internal static partial class ConcurrencyRules
{
    /// <summary>PSH1317 — a call drops a cancellation token that the call site is holding.</summary>
    public static readonly DiagnosticDescriptor PassCancellationToken = Create(
        "PSH1317",
        "Pass the cancellation token to the call that accepts one",
        "Pass '{0}' to '{1}'",
        PassCancellationTokenDescription);

    /// <summary>The PassCancellationToken rule description.</summary>
    private const string PassCancellationTokenDescription =
        "A cancellation token is in scope and the call can carry it — either its own token parameter was left at its default, or "
        + "an overload takes one — so cancellation stops here. Everything downstream keeps running after the caller has given up: "
        + "the thread pool worker is still occupied, the connection or file handle is still held, and the result is discarded when "
        + "it finally arrives. Only an overload that binds to the arguments already written and returns the same type is suggested, "
        + "so a call that has no cancellable form is never reported, and a call that already passes a token — including an explicit "
        + "'CancellationToken.None' — is left alone.";
}
