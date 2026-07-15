// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2446 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2446 — a stream read's returned byte count is awaited and discarded.</summary>
    public static readonly DiagnosticDescriptor DiscardedStreamRead = Create(
        "SST2446",
        "The number of bytes read from a stream should not be discarded",
        "This read discards the number of bytes returned, so a short read is silently accepted; {0}",
        DiscardedStreamReadDescription);

    /// <summary>The DiscardedStreamRead rule description.</summary>
    private const string DiscardedStreamReadDescription =
        "An asynchronous stream read is allowed to return fewer bytes than were asked for — a short read is normal on a network "
        + "stream, a pipe, or a file still being written — and the count it returns is the only signal of how much of the buffer was "
        + "actually filled. Awaiting the read and throwing that count away treats every read as complete, so the buffer keeps stale or "
        + "default bytes past the point the read reached, and the data that was not read is silently lost or misparsed. Only two shapes "
        + "are reported, because both discard the count while looking like correct asynchronous code: a read awaited through a configured "
        + "awaiter, and a read whose value is stored in a local and then awaited as a statement. A read whose count is assigned, returned, "
        + "compared, or otherwise used is never reported.";
}
