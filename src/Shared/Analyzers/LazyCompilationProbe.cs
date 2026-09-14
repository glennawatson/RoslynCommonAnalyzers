// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>Answers one yes-or-no question about a compilation on first demand and caches the answer, including a negative one.</summary>
internal sealed class LazyCompilationProbe
{
    /// <summary>The state before the predicate has answered.</summary>
    private const int Unresolved = 0;

    /// <summary>The state once the predicate has answered <see langword="false"/>.</summary>
    private const int Failed = 1;

    /// <summary>The state once the predicate has answered <see langword="true"/>.</summary>
    private const int Passed = 2;

    /// <summary>The compilation the predicate is asked about.</summary>
    private readonly Compilation _compilation;

    /// <summary>The question asked of the compilation.</summary>
    private readonly Func<Compilation, bool> _predicate;

    /// <summary>Serializes the first answer, or null when concurrent first demands may each run the predicate.</summary>
    private readonly object? _gate;

    /// <summary>The published answer, or <see cref="Unresolved"/> before the first demand.</summary>
    private int _state;

    /// <summary>Initializes a new instance of the <see cref="LazyCompilationProbe"/> class.</summary>
    /// <param name="compilation">The compilation the predicate is asked about.</param>
    /// <param name="predicate">The question asked of the compilation.</param>
    /// <param name="gate">The lock serializing the first answer, or null for none.</param>
    private LazyCompilationProbe(Compilation compilation, Func<Compilation, bool> predicate, object? gate)
    {
        _compilation = compilation;
        _predicate = predicate;
        _gate = gate;
    }

    /// <summary>Creates a probe whose concurrent first demands may each run the predicate.</summary>
    /// <param name="compilation">The compilation the predicate is asked about.</param>
    /// <param name="predicate">The question asked of the compilation.</param>
    /// <returns>The probe.</returns>
    internal static LazyCompilationProbe Create(Compilation compilation, Func<Compilation, bool> predicate) =>
        new(compilation, predicate, gate: null);

    /// <summary>Creates a probe whose predicate runs at most once, however many callbacks ask at the same time.</summary>
    /// <param name="compilation">The compilation the predicate is asked about.</param>
    /// <param name="predicate">The question asked of the compilation.</param>
    /// <returns>The probe.</returns>
    internal static LazyCompilationProbe CreateSynchronized(Compilation compilation, Func<Compilation, bool> predicate) =>
        new(compilation, predicate, new object());

    /// <summary>Gets the answer, running the predicate on first demand.</summary>
    /// <returns>The predicate's answer for the compilation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Get()
    {
        var state = Volatile.Read(ref _state);
        return state == Unresolved ? Resolve() : state == Passed;
    }

    /// <summary>Runs the predicate and publishes its answer, under the gate when there is one.</summary>
    /// <returns>The predicate's answer for the compilation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool Resolve()
    {
        if (_gate is null)
        {
            return Publish();
        }

        lock (_gate)
        {
            var state = _state;
            return state == Unresolved ? Publish() : state == Passed;
        }
    }

    /// <summary>Runs the predicate and publishes its answer.</summary>
    /// <returns>The predicate's answer for the compilation.</returns>
    private bool Publish()
    {
        var passed = _predicate(_compilation);
        Volatile.Write(ref _state, passed ? Passed : Failed);
        return passed;
    }
}
