// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2469 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2469 — a write through an extension block's by-value struct receiver is discarded.</summary>
    public static readonly DiagnosticDescriptor DiscardedReceiverWrite = Create(
        "SST2469",
        "A write through a by-value struct receiver is discarded",
        "'{0}' is a struct passed by value, so this write to it is made to a copy and is lost when the member returns",
        DiscardedReceiverWriteDescription);

    /// <summary>The DiscardedReceiverWrite rule description.</summary>
    private const string DiscardedReceiverWriteDescription =
        "An extension block's receiver is an ordinary parameter. When its type is a struct and it is passed by value, the member receives a copy: "
        + "assigning to a field or property of it changes the copy and the caller's value is untouched, so 'point.Doubled = 10' silently leaves "
        + "'point' as it was. Nothing reports this — the code compiles, the setter runs, and the write simply goes nowhere, which makes it read as a "
        + "working mutable property right up until someone relies on it. Declaring the receiver 'ref' makes the write land on the caller's value; if "
        + "the member is not meant to mutate, the accessor that writes should not exist. A receiver passed by 'ref' is never reported, and neither is "
        + "a class receiver, where the copy is of the reference and the write reaches the same object.";
}
