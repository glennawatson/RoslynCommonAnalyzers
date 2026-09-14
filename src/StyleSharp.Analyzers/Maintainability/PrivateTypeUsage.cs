// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Collects the private member candidates and references of one type symbol across its declarations.</summary>
/// <remarks>
/// The compilation-end action reads both collections only after every action that fills them has run, so the pair is
/// never observed half-written and each addition needs only its own thread safety.
/// </remarks>
internal sealed class PrivateTypeUsage
{
    /// <summary>The collected private member candidates.</summary>
    private readonly ConcurrentQueue<PrivateMemberCandidate> _candidates = new();

    /// <summary>The collected member references.</summary>
    private readonly ConcurrentQueue<PrivateMemberReference> _references = new();

    /// <summary>Adds one private member candidate.</summary>
    /// <param name="candidate">The candidate to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddMemberCandidate(PrivateMemberCandidate candidate) => _candidates.Enqueue(candidate);

    /// <summary>Adds one member reference.</summary>
    /// <param name="reference">The reference to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddMemberReference(PrivateMemberReference reference) => _references.Enqueue(reference);

    /// <summary>Copies the candidates collected so far.</summary>
    /// <returns>The candidates.</returns>
    internal List<PrivateMemberCandidate> SnapshotCandidates() => new(_candidates.ToArray());

    /// <summary>Copies the references collected so far.</summary>
    /// <returns>The references.</returns>
    internal List<PrivateMemberReference> SnapshotReferences() => new(_references.ToArray());
}
