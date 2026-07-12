// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyInParameter = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1007PassLargeReadonlyStructByInAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1007 (pass large readonly structs by 'in' reference).</summary>
public class PassLargeReadonlyStructByInAnalyzerUnitTest
{
    /// <summary>The struct declarations the tests measure against.</summary>
    /// <remarks>
    /// <c>Snapshot</c> is 40 bytes and <c>Reading</c> is the same size expressed as auto-properties, which
    /// only counts if the estimator sees their synthesized backing fields. <c>Small</c> is 16 bytes — the
    /// register-passing boundary — and <c>Mutable</c> is large but not <c>readonly</c>.
    /// </remarks>
    private const string Structs = """

        public readonly struct Snapshot
        {
            public readonly long A;
            public readonly long B;
            public readonly long C;
            public readonly long D;
            public readonly long E;
        }

        public readonly struct Reading
        {
            public long A { get; init; }
            public long B { get; init; }
            public long C { get; init; }
            public long D { get; init; }
            public long E { get; init; }
        }

        public readonly struct Small
        {
            public readonly long A;
            public readonly long B;
        }

        public struct Mutable
        {
            public long A;
            public long B;
            public long C;
            public long D;
            public long E;
        }
        """;

    /// <summary>Verifies a large readonly struct passed by value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LargeReadonlyStructPassedByValueIsReportedAsync()
        => await VerifyAsync(
            """
            internal static class C
            {
                internal static long Score(Snapshot {|PSH1007:snapshot|}) => snapshot.A;
            }
            """);

    /// <summary>Verifies the estimator measures a struct built from auto-properties.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AutoPropertyStructIsMeasuredAsync()
        => await VerifyAsync(
            """
            internal static class C
            {
                internal static long Score(Reading {|PSH1007:reading|}) => reading.A;
            }
            """);

    /// <summary>Verifies a struct at the register-passing boundary is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SmallStructIsCleanAsync()
        => await VerifyAsync(
            """
            internal static class C
            {
                internal static long Score(Small small) => small.A;

                internal static int Count(int value) => value;
            }
            """);

    /// <summary>Verifies a struct that is not readonly is left to PSH1003.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>An 'in' here would take a defensive copy at every member access, which is worse than the copy it saves.</remarks>
    [Test]
    public async Task MutableStructIsCleanAsync()
        => await VerifyAsync(
            """
            internal static class C
            {
                internal static long Score(Mutable mutable) => mutable.A;
            }
            """);

    /// <summary>Verifies ref structs are never reported, whatever their size.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RefStructIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            internal readonly ref struct BigRef
            {
                public readonly long A;
                public readonly long B;
                public readonly long C;
                public readonly long D;
                public readonly long E;
            }

            internal static class C
            {
                internal static int Length(ReadOnlySpan<byte> span) => span.Length;

                internal static int Count(Span<int> span) => span.Length;

                internal static long Score(BigRef value) => value.A;
            }
            """);

    /// <summary>Verifies the SIMD types are excluded even though they are large readonly structs.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The whole BCL vector surface passes these by value; an 'in' buys nothing and costs an indirection.</remarks>
    [Test]
    public async Task SimdTypesAreCleanAsync()
        => await VerifyAsync(
            """
            using System.Numerics;
            using System.Runtime.Intrinsics;

            internal static class C
            {
                internal static float First(Vector256<float> vector) => vector.GetElement(0);

                internal static float Wide(Vector512<float> vector) => vector.GetElement(0);

                internal static float Numeric(Vector<float> vector) => vector[0];

                internal static float Transform(Matrix4x4 matrix) => matrix.M11;
            }
            """);

    /// <summary>Verifies the cheap framework handle types are excluded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FrameworkHandleTypesAreCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Threading;

            internal static class C
            {
                internal static bool Cancelled(CancellationToken token) => token.IsCancellationRequested;

                internal static int Length(ReadOnlyMemory<byte> memory) => memory.Length;

                internal static bool Done(Guid id) => id == Guid.Empty;
            }
            """);

    /// <summary>Verifies an externally visible signature is not reported by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Adding 'in' to a public member is a binary break for every compiled consumer.</remarks>
    [Test]
    public async Task PublicApiIsCleanByDefaultAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public long Score(Snapshot snapshot) => snapshot.A;

                protected internal long Guarded(Snapshot snapshot) => snapshot.A;

                internal long Hidden(Snapshot {|PSH1007:snapshot|}) => snapshot.A;
            }
            """);

    /// <summary>Verifies an externally visible signature is reported once it is opted in.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicApiIsReportedWhenOptedInAsync()
        => await VerifyWithConfigAsync(
            """
            public static class C
            {
                public static long Score(Snapshot {|PSH1007:snapshot|}) => snapshot.A;
            }
            """,
            "performancesharp.PSH1007.in_parameter_include_public_api = true");

    /// <summary>Verifies a signature an interface or a base type dictates is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritedSignaturesAreCleanAsync()
        => await VerifyAsync(
            """
            internal interface IScorer
            {
                long Score(Snapshot snapshot);
            }

            internal abstract class Base
            {
                internal abstract long Rank(Snapshot snapshot);

                internal virtual long Weigh(Snapshot snapshot) => snapshot.A;
            }

            internal sealed class Scorer : Base, IScorer
            {
                public long Score(Snapshot snapshot) => snapshot.A;

                internal override long Rank(Snapshot snapshot) => snapshot.B;
            }
            """);

    /// <summary>Verifies an async method and an iterator are not reported, because neither may take 'in'.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncAndIteratorAreCleanAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Threading.Tasks;

            internal static class C
            {
                internal static async Task<long> ScoreAsync(Snapshot snapshot)
                {
                    await Task.Yield();
                    return snapshot.A;
                }

                internal static IEnumerable<long> Enumerate(Snapshot snapshot)
                {
                    yield return snapshot.A;
                    yield return snapshot.B;
                }
            }
            """);

    /// <summary>Verifies a captured parameter is not reported, because a reference cannot be captured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapturedParameterIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            internal static class C
            {
                internal static Func<long> Defer(Snapshot snapshot) => () => snapshot.A;

                internal static long Local(Snapshot snapshot)
                {
                    long Read() => snapshot.B;
                    return Read();
                }
            }
            """);

    /// <summary>Verifies a parameter the body writes to is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WrittenParameterIsCleanAsync()
        => await VerifyAsync(
            """
            internal static class C
            {
                internal static long Reassign(Snapshot snapshot)
                {
                    snapshot = default;
                    return snapshot.A;
                }

                internal static long ByReference(Snapshot snapshot)
                {
                    Replace(ref snapshot);
                    return snapshot.A;
                }

                private static void Replace(ref Snapshot snapshot) => snapshot = default;
            }
            """);

    /// <summary>Verifies a parameter that only reads the value inside a lambda's own scope still reports.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>A lambda that never touches the parameter does not capture it, so the change still compiles.</remarks>
    [Test]
    public async Task UncapturedLambdaStillReportsAsync()
        => await VerifyAsync(
            """
            using System;

            internal static class C
            {
                internal static long Score(Snapshot {|PSH1007:snapshot|})
                {
                    Func<long> constant = () => 1L;
                    return snapshot.A + constant();
                }
            }
            """);

    /// <summary>Verifies a constructor and a local function are measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorAndLocalFunctionAreMeasuredAsync()
        => await VerifyAsync(
            """
            internal sealed class C
            {
                internal C(Snapshot {|PSH1007:snapshot|}) => Total = snapshot.A;

                internal long Total { get; }

                internal static long Run()
                {
                    long Score(Snapshot {|PSH1007:snapshot|}) => snapshot.B;
                    return Score(default);
                }
            }
            """);

    /// <summary>Verifies a lambda, a delegate, and a primary constructor are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateShapesAndPrimaryConstructorsAreCleanAsync()
        => await VerifyAsync(
            """
            using System;

            internal delegate long Scorer(Snapshot snapshot);

            internal sealed class Holder(Snapshot snapshot)
            {
                internal long Total => snapshot.A;
            }

            internal static class C
            {
                internal static Scorer Make() => snapshot => snapshot.A;

                internal static Func<Snapshot, long> Lambda() => static snapshot => snapshot.B;
            }
            """);

    /// <summary>Verifies an attribute constructor is not reported, because every use of the attribute would break.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AttributeConstructorIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            internal sealed class MarkAttribute : Attribute
            {
                internal MarkAttribute(Snapshot snapshot) => Total = snapshot.A;

                internal long Total { get; }
            }
            """);

    /// <summary>Verifies a parameter that already carries a modifier is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExistingModifiersAreCleanAsync()
        => await VerifyAsync(
            """
            internal static class C
            {
                internal static long Already(in Snapshot snapshot) => snapshot.A;

                internal static long Mutable(ref Snapshot snapshot) => snapshot.A;

                internal static long Extension(this Snapshot snapshot) => snapshot.A;
            }
            """);

    /// <summary>Verifies the size threshold is configurable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MinimumSizeIsConfigurableAsync()
        => await VerifyWithConfigAsync(
            """
            internal readonly struct Medium
            {
                public readonly long A;
                public readonly long B;
                public readonly long C;
            }

            internal static class C
            {
                internal static long Score(Medium {|PSH1007:medium|}) => medium.A;
            }
            """,
            "performancesharp.PSH1007.in_parameter_minimum_size = 24");

    /// <summary>Verifies a configured size below the ABI floor is raised to it rather than honored.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>A 16-byte struct rides in registers; an 'in' would force it to memory, so the floor is not configurable away.</remarks>
    [Test]
    public async Task SizeFloorIsEnforcedAsync()
        => await VerifyWithConfigAsync(
            """
            internal static class C
            {
                internal static long Score(Small small) => small.A;
            }
            """,
            "performancesharp.PSH1007.in_parameter_minimum_size = 8");

    /// <summary>Verifies a configured type exclusion is honored.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExcludedTypesAreHonoredAsync()
        => await VerifyWithConfigAsync(
            """
            internal static class C
            {
                internal static long Score(Snapshot snapshot) => snapshot.A;

                internal static long Other(Reading {|PSH1007:reading|}) => reading.A;
            }
            """,
            "performancesharp.PSH1007.in_parameter_excluded_types = Snapshot");

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyInParameter.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + Structs,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer verification with one editorconfig setting applied.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="setting">The editorconfig line to apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyWithConfigAsync(string source, string setting)
    {
        var test = new VerifyInParameter.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + Structs,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", $"""
            root = true
            [*.cs]
            {setting}

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
