// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Descriptor for PSH1506 — the HTTP request or response body is read or written with a synchronous
/// stream call on the request thread.
/// </summary>
internal static partial class AspNetCoreRules
{
    /// <summary>PSH1506 — a synchronous read or write of the HTTP request/response body blocks the request thread.</summary>
    public static readonly DiagnosticDescriptor SynchronousBodyIo = Create(
        "PSH1506",
        "Synchronous read or write of the HTTP request or response body",
        "This reads or writes the HTTP body synchronously on the request thread; await '{0}' instead so the thread is released while the I/O is in flight",
        SynchronousBodyIoDescription);

    /// <summary>The PSH1506 rule description.</summary>
    private const string SynchronousBodyIoDescription =
        "Reading or writing the HTTP request or response body with a synchronous stream call — 'Read', 'ReadByte', 'Write', "
        + "'WriteByte', 'Flush' or 'CopyTo' on the body stream, or 'ReadToEnd', 'ReadLine' or 'ReadBlock' on a reader that wraps "
        + "it — blocks the pooled thread that was handling the request. The body is backed by the network connection, so the call "
        + "waits on socket I/O while holding that thread; under load the pool runs out of threads and every request slows down. The "
        + "synchronous form also buffers the payload without a bound, so a large or slow transfer is held in memory in full. ASP.NET "
        + "Core disables synchronous request/response body I/O by default, so the call additionally throws at runtime unless that "
        + "safeguard is turned off. Awaiting the asynchronous overload instead ('ReadAsync', 'WriteAsync', 'FlushAsync', "
        + "'ReadToEndAsync', and their siblings) yields the thread back to the pool while the I/O is outstanding. The suggested "
        + "overload is resolved on the receiver, so nothing is reported when the framework does not expose one, and the rule costs "
        + "nothing outside an ASP.NET Core project.";
}
