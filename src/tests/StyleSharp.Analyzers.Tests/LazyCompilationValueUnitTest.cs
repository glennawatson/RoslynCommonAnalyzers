// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared first-demand value computed from a compilation.</summary>
public sealed class LazyCompilationValueUnitTest
{
    /// <summary>How long a first demand holding the value gives a concurrent demand to finish.</summary>
    private static readonly TimeSpan ContenderWait = TimeSpan.FromMilliseconds(100);

    /// <summary>A compilation that sees the platform and declares nothing.</summary>
    private static readonly CSharpCompilation PlatformCompilation = CSharpCompilation.Create(
        nameof(LazyCompilationValueUnitTest),
        references: RuntimeMetadataReferences.Platform);

    /// <summary>Verifies the resolver is given the compilation the holder was created for.</summary>
    /// <param name="runOnce">Whether the holder serializes its first computation.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ComputesFromTheCompilationAsync(bool runOnce)
    {
        var holder = new LazyCompilationValue<INamedTypeSymbol?>(PlatformCompilation, static compilation => compilation.GetTypeByMetadataName("System.String"), runOnce);

        await Assert.That(holder.Get()).IsSameReferenceAs(PlatformCompilation.GetTypeByMetadataName("System.String"));
    }

    /// <summary>Verifies the resolver waits for the first demand, runs once, and a null result is cached like any other.</summary>
    /// <param name="runOnce">Whether the holder serializes its first computation.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RunsResolverOnceAndCachesNullAsync(bool runOnce)
    {
        var calls = 0;
        var holder = new LazyCompilationValue<string?>(
            PlatformCompilation,
            _ =>
            {
                calls++;
                return null;
            },
            runOnce);

        await Assert.That(calls).IsEqualTo(0);
        await Assert.That(holder.Get()).IsNull();
        await Assert.That(holder.Get()).IsNull();
        await Assert.That(calls).IsEqualTo(1);
    }

    /// <summary>Verifies a value type result is cached, including its default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CachesValueTypeResultsAsync()
    {
        var calls = 0;
        var holder = new LazyCompilationValue<int>(
            PlatformCompilation,
            _ =>
            {
                calls++;
                return 0;
            });

        await Assert.That(holder.Get()).IsEqualTo(0);
        await Assert.That(holder.Get()).IsEqualTo(0);
        await Assert.That(calls).IsEqualTo(1);
    }

    /// <summary>Verifies a run-once holder makes a concurrent demand wait for the first value rather than compute it again.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RunOnceHolderMakesConcurrentDemandWaitAsync()
    {
        var calls = 0;
        var contenderFinishedEarly = true;
        Task<string>? contender = null;
        LazyCompilationValue<string>? holder = null;
        holder = new(
            PlatformCompilation,
            compilation =>
            {
                _ = Interlocked.Increment(ref calls);
                if (contender is null)
                {
                    var started = Task.Run(() => holder!.Get());
                    contender = started;
                    contenderFinishedEarly = SpinWait.SpinUntil(() => started.IsCompleted, ContenderWait);
                }

                return "resolved";
            },
            runOnce: true);

        var first = holder.Get();
        var second = await contender!;

        await Assert.That(first).IsEqualTo("resolved");
        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(contenderFinishedEarly).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
    }
}
