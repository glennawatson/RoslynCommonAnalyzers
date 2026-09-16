// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Collects the private member candidates and references of one type symbol across its declarations.</summary>
/// <remarks>
/// A type declared in one place is filled and read by the same callback, so it takes the lists unsynchronised. A
/// partial type is filled by one callback per part and read by the compilation-end action once they have all run, so
/// only each addition needs the gate, and the gate stays off the single-part path.
/// </remarks>
internal sealed class PrivateTypeUsage
{
    /// <summary>The collected private member candidates.</summary>
    private readonly List<PrivateMemberCandidate> _candidates = [];

    /// <summary>The collected member references.</summary>
    private readonly List<PrivateMemberReference> _references = [];

    /// <summary>Serializes additions, or null when one callback fills this instance.</summary>
    private readonly object? _gate;

    /// <summary>Initializes a new instance of the <see cref="PrivateTypeUsage"/> class.</summary>
    /// <param name="shared">Whether more than one callback fills this instance.</param>
    internal PrivateTypeUsage(bool shared) => _gate = shared ? new object() : null;

    /// <summary>Gets the collected candidates.</summary>
    internal List<PrivateMemberCandidate> Candidates => _candidates;

    /// <summary>Gets the collected references.</summary>
    internal List<PrivateMemberReference> References => _references;

    /// <summary>Adds one private member candidate.</summary>
    /// <param name="candidate">The candidate to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddMemberCandidate(PrivateMemberCandidate candidate)
    {
        if (_gate is { } gate)
        {
            AddShared(gate, _candidates, candidate);
            return;
        }

        _candidates.Add(candidate);
    }

    /// <summary>Adds one member reference.</summary>
    /// <param name="reference">The reference to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddMemberReference(PrivateMemberReference reference)
    {
        if (_gate is { } gate)
        {
            AddShared(gate, _references, reference);
            return;
        }

        _references.Add(reference);
    }

    /// <summary>Adds one item under the gate.</summary>
    /// <typeparam name="T">The collected item's type.</typeparam>
    /// <param name="gate">The gate serializing additions.</param>
    /// <param name="items">The list to add to.</param>
    /// <param name="item">The item to add.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AddShared<T>(object gate, List<T> items, T item)
    {
        lock (gate)
        {
            items.Add(item);
        }
    }
}
