// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>Computes one value from a compilation on first demand and caches it, including a null or default result.</summary>
/// <typeparam name="T">The cached value's type.</typeparam>
/// <remarks>
/// The fast path is a field read and an array index. By default concurrent first demands may each run the resolver and
/// the first published result wins, which suits a resolver of a few metadata lookups. With <c>runOnce</c> the resolver
/// runs at most once, under a lock that stays off the fast path.
/// </remarks>
internal sealed class LazyCompilationValue<T>
{
    /// <summary>The compilation the value is computed from.</summary>
    private readonly Compilation _compilation;

    /// <summary>Computes the value.</summary>
    private readonly Func<Compilation, T> _resolve;

    /// <summary>Serializes the first computation, or null when concurrent first demands may each run the resolver.</summary>
    private readonly object? _gate;

    /// <summary>A single slot holding the published value; null until first demand.</summary>
    private T[]? _resolved;

    /// <summary>Initializes a new instance of the <see cref="LazyCompilationValue{T}"/> class.</summary>
    /// <param name="compilation">The compilation the value is computed from.</param>
    /// <param name="resolve">Computes the value.</param>
    /// <param name="runOnce">Whether the resolver must run at most once however many callbacks ask at the same time.</param>
    internal LazyCompilationValue(Compilation compilation, Func<Compilation, T> resolve, bool runOnce = false)
    {
        _compilation = compilation;
        _resolve = resolve;
        _gate = runOnce ? new object() : null;
    }

    /// <summary>Gets the value, computing it on first demand.</summary>
    /// <returns>The value the resolver produced for the compilation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T Get() => (Volatile.Read(ref _resolved) ?? Resolve())[0];

    /// <summary>Computes and publishes the value, under the gate when there is one.</summary>
    /// <returns>The published slot.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private T[] Resolve()
    {
        if (_gate is null)
        {
            T[] computed = [_resolve(_compilation)];
            return Interlocked.CompareExchange(ref _resolved, computed, null) ?? computed;
        }

        lock (_gate)
        {
            var resolved = _resolved;
            if (resolved is null)
            {
                resolved = [_resolve(_compilation)];
                Volatile.Write(ref _resolved, resolved);
            }

            return resolved;
        }
    }
}
