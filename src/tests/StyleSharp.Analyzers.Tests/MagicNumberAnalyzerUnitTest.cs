// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyMagicNumber = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1471MagicNumberAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1471 (magic numbers should be named constants).</summary>
public class MagicNumberAnalyzerUnitTest
{
    /// <summary>The path the analyzer config file is added at in the test workspace.</summary>
    private const string EditorConfigPath = "/.editorconfig";

    /// <summary>Verifies settings initialized for one tree do not leak into another tree's slot.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SeparateTreesKeepTheirOwnAllowedValuesAsync()
    {
        var test = new VerifyMagicNumber.Test();
        test.TestState.Sources.Add(("/Allowed.cs", """
            public class Allowed
            {
                public int First(int value) => value + 2;
                public int Second(int value) => value * 2;
            }
            """));
        test.TestState.Sources.Add(("/Default.cs", """
            public class Default
            {
                public int First(int value) => value + {|SST1471:2|};
                public int Second(int value) => value * {|SST1471:2|};
            }
            """));
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, """
            root = true
            [Allowed.cs]
            stylesharp.SST1471.magic_number_allowed_values = -1, 0, 1, 2

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a bare literal in an expression is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LiteralInExpressionIsReportedAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool IsExpired(int age) => age > {|SST1471:90|};
            }
            """);

    /// <summary>Verifies a positional capacity argument is reported by default.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>Labelling it is the documented way to say what it means, so the bare form still asks for one.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PositionalCapacityIsReportedByDefaultAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public List<int> Bare() => new List<int>({|SST1471:4|});

                public List<int> Labelled() => new List<int>(capacity: 4);
            }
            """);

    /// <summary>Verifies a project can opt into accepting a positional capacity argument.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PositionalCapacityIsCleanWhenAllowedAsync()
    {
        var test = new VerifyMagicNumber.Test
        {
            TestCode = """
                       using System.Collections.Generic;
                       using System.Text;

                       public class C
                       {
                           public List<int> Numbers() => new List<int>(4);

                           public StringBuilder Builder() => new StringBuilder(256);

                           public bool Stale(int age) => age > {|SST1471:90|};
                       }
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.SST1471.allow_capacity_arguments = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the allow-listed values need no name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AllowedValuesAreCleanAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Any(int count) => count > 0;

                public bool One(int count) => count == 1;

                public int NotFound() => -1;

                public bool Zero(double value) => value == 0.0;
            }
            """);

    /// <summary>Verifies a negative literal is measured after folding the unary minus.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NegativeLiteralIsFoldedBeforeComparingAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Below(int value) => value < {|SST1471:-5|};

                public bool NotFound(int value) => value == -1;
            }
            """);

    /// <summary>Verifies a literal that names itself at a declaration is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamedDeclarationSitesAreCleanAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            using System;

            public enum Level { High = 30 }

            [System.AttributeUsage(AttributeTargets.Class)]
            public sealed class LimitAttribute : Attribute
            {
                public LimitAttribute(int max) => Max = max;

                public int Max { get; }
            }

            [Limit(500)]
            public class C
            {
                private const int Threshold = 90;

                private static readonly TimeSpan Retry = TimeSpan.FromSeconds(30);

                private readonly int[] _primes = { 2, 3, 5, 7 };

                private int _active = 12;

                public int Capacity { get; } = 64;

                public int Scaled(int value = 25)
                {
                    const int Factor = 60;
                    var seconds = 45;
                    return (value * Factor) + seconds + Threshold + _active + (int)Retry.TotalSeconds + _primes.Length + Capacity;
                }
            }
            """);

    /// <summary>Verifies a literal inside a non-bare initializer is still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The local names the task, not the delay, so the delay stays unexplained.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LiteralInsideNonBareInitializerIsReportedAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public Task Wait()
                {
                    var task = Task.Delay({|SST1471:500|});
                    return task;
                }
            }
            """);

    /// <summary>Verifies a literal in a lambda does not inherit the enclosing field's name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LiteralInsideLambdaUnderReadonlyFieldIsReportedAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                private static readonly Func<int> Compute = () => {|SST1471:500|};

                public int Run() => Compute();
            }
            """);

    /// <summary>Verifies bit patterns and shift distances state their own meaning.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BitPatternsAndShiftDistancesAreCleanAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                public uint Mix(uint hash)
                {
                    hash ^= hash >> 16;
                    hash &= 0xFF00FF;
                    hash |= 0b1010;
                    hash <<= 13;
                    return hash << 7;
                }
            }
            """);

    /// <summary>Verifies a cardinality guard against a count or a length is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CardinalityGuardsAreCleanAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public bool Pair(List<int> items) => items.Count == 2;

                public bool Short(string text) => text.Length < 3;

                public bool Wide(int[,] grid) => grid.Rank >= 2;

                public bool Reversed(string text) => 4 > text.Length;
            }
            """);

    /// <summary>Verifies a named argument, an array size and a stackalloc length are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LabelledAndBufferLengthsAreCleanAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public List<int> Make() => new List<int>(capacity: 4);

                public byte[] Buffer() => new byte[16];

                public int Scratch()
                {
                    Span<char> span = stackalloc char[32];
                    return span.Length;
                }
            }
            """);

    /// <summary>Verifies the hash-mixing primes in a GetHashCode body are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GetHashCodeBodyIsCleanAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public override int GetHashCode() => (_value * 397) ^ 17;
            }
            """);

    /// <summary>Verifies a mixing prime held in a local inside <c>GetHashCode</c> is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The declaration does not name the value, so the walk must reach the method that excuses it.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GetHashCodeLocalIsCleanAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly string _name = string.Empty;

                public override int GetHashCode()
                {
                    var valueHashCode = _name.Length == 0 ? 1963 : _name.GetHashCode();
                    return valueHashCode ^ 397;
                }
            }
            """);

    /// <summary>Verifies a local inside an ordinary method is still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalInsideOrdinaryMethodIsReportedAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Delay()
                {
                    var timeout = {|SST1471:5000|} + 1;
                    return timeout;
                }
            }
            """);

    /// <summary>Verifies arguments to a positional BCL constructor are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PositionalConstructorArgumentsAreCleanAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public DateTime Epoch() => new DateTime(2025, 1, 1);

                public Version Current() => new Version(3, 17, 7);
            }
            """);

    /// <summary>Verifies a static factory does not inherit the positional constructor exemption.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The method names the unit; the magnitude is still policy that deserves a name.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DurationFactoryArgumentIsReportedAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public TimeSpan Timeout() => TimeSpan.FromMinutes({|SST1471:30|});
            }
            """);

    /// <summary>Verifies a case label and a constant pattern are reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CaseLabelsAndPatternsAreReportedAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string Name(int state) => state switch
                {
                    0 => "start",
                    {|SST1471:3|} => "done",
                    _ => "other",
                };

                public bool Many(int count) => count is > {|SST1471:2|};
            }
            """);

    /// <summary>Verifies the allow-list is configurable through .editorconfig.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConfiguredAllowListSuppressesTheDiagnosticAsync()
    {
        var test = new VerifyMagicNumber.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Half(int value) => value / 2;

                           public int Third(int value) => value / {|SST1471:3|};
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.SST1471.magic_number_allowed_values = -1, 0, 1, 2

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an unparsable allow-list falls back to the defaults instead of flagging everything.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnparsableAllowListFallsBackToDefaultsAsync()
    {
        var test = new VerifyMagicNumber.Test
        {
            TestCode = """
                       public class C
                       {
                           public bool Any(int count) => count > 0;

                           public bool Big(int count) => count > {|SST1471:7|};
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.SST1471.magic_number_allowed_values = nonsense

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an index into an element access is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The literal is a slot, the same positional shape as an array rank, which is already exempt. Naming it
    /// can only produce a constant that restates the number it holds.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ElementAccessIndexIsNotReportedAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int[] values) => values[3] + values[7];
            }
            """);

    /// <summary>Verifies the elements of a collection that is a declaration's whole value are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// <c>var timeout = 500;</c> is exempt because the name explains the number, and this is that statement
    /// three times over — the name explains the whole list. Reporting some of the elements was the odd part:
    /// it pointed at the 2 and the 3 and left the 1 alone, because 1 is allowlisted.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CollectionThatIsAWholeInitializerIsNotReportedAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static readonly int[] Offsets = [0, 4, 8];

                private static readonly int[] Legacy = new[] { 2, 3 };

                public int M() => Offsets.Length + Legacy.Length;
            }
            """);

    /// <summary>Verifies a literal inside a collection passed straight to a call is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The exemption is the declaration's name explaining the list; an argument has no such name.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CollectionPassedAsAnArgumentIsStillReportedAsync() =>
        VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M() => Sum([{|SST1471:4|}, {|SST1471:8|}]);

                private static int Sum(int[] values) => values.Length;
            }
            """);
}
