// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared first-demand answer to one yes-or-no question about a compilation.</summary>
public sealed class LazyCompilationProbeUnitTest
{
    /// <summary>How long a first demand holding the answer gives a concurrent demand to finish.</summary>
    private static readonly TimeSpan ContenderWait = TimeSpan.FromMilliseconds(100);

    /// <summary>A compilation that sees the platform and declares nothing.</summary>
    private static readonly CSharpCompilation PlatformCompilation = CSharpCompilation.Create(
        nameof(LazyCompilationProbeUnitTest),
        references: RuntimeMetadataReferences.Platform);

    /// <summary>Verifies the predicate is asked about the compilation the probe was created for.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnswersForTheCompilationAsync()
    {
        await Assert.That(LazyCompilationProbe.Create(PlatformCompilation, HasString).Get()).IsTrue();
        await Assert.That(LazyCompilationProbe.CreateSynchronized(PlatformCompilation, HasString).Get()).IsTrue();
        await Assert.That(LazyCompilationProbe.Create(PlatformCompilation, HasMissingType).Get()).IsFalse();
        await Assert.That(LazyCompilationProbe.CreateSynchronized(PlatformCompilation, HasMissingType).Get()).IsFalse();
    }

    /// <summary>Verifies the predicate waits for the first demand, runs once, and every later demand gets its answer.</summary>
    /// <param name="answer">The answer the predicate gives.</param>
    /// <param name="synchronized">Whether the probe serializes its first answer.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(true, false)]
    [Arguments(false, false)]
    [Arguments(true, true)]
    [Arguments(false, true)]
    public async Task RunsPredicateOnceOnFirstDemandAsync(bool answer, bool synchronized)
    {
        var calls = 0;
        Func<Compilation, bool> predicate = _ =>
        {
            calls++;
            return answer;
        };

        var probe = synchronized
            ? LazyCompilationProbe.CreateSynchronized(PlatformCompilation, predicate)
            : LazyCompilationProbe.Create(PlatformCompilation, predicate);

        await Assert.That(calls).IsEqualTo(0);
        await Assert.That(probe.Get()).IsEqualTo(answer);
        await Assert.That(probe.Get()).IsEqualTo(answer);
        await Assert.That(calls).IsEqualTo(1);
    }

    /// <summary>Verifies a synchronized probe makes a concurrent demand wait for the first answer rather than run the predicate again.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SynchronizedProbeMakesConcurrentDemandWaitAsync()
    {
        var calls = 0;
        var contenderFinishedEarly = true;
        Task<bool>? contender = null;
        LazyCompilationProbe? probe = null;
        probe = LazyCompilationProbe.CreateSynchronized(PlatformCompilation, compilation =>
        {
            _ = Interlocked.Increment(ref calls);
            if (contender is null)
            {
                var started = Task.Run(() => probe!.Get());
                contender = started;
                contenderFinishedEarly = SpinWait.SpinUntil(() => started.IsCompleted, ContenderWait);
            }

            return true;
        });

        var first = probe.Get();
        var second = await contender!;

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsTrue();
        await Assert.That(contenderFinishedEarly).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
    }

    /// <summary>Returns whether a compilation sees <see cref="string"/>.</summary>
    /// <param name="compilation">The compilation to ask.</param>
    /// <returns><see langword="true"/> when the type resolves.</returns>
    private static bool HasString(Compilation compilation) => compilation.GetTypeByMetadataName("System.String") is not null;

    /// <summary>Returns whether a compilation sees a type no reference declares.</summary>
    /// <param name="compilation">The compilation to ask.</param>
    /// <returns><see langword="true"/> when the type resolves.</returns>
    private static bool HasMissingType(Compilation compilation) => compilation.GetTypeByMetadataName("Missing.Type") is not null;
}
