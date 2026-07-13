// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyMagicNumber = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1471MagicNumberAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1471 (magic numbers should be named constants).</summary>
public class MagicNumberAnalyzerUnitTest
{
    /// <summary>Verifies a bare literal in an expression is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LiteralInExpressionIsReportedAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool IsExpired(int age) => age > {|SST1471:90|};
            }
            """);

    /// <summary>Verifies the allow-listed values need no name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AllowedValuesAreCleanAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
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
    [Test]
    public async Task NegativeLiteralIsFoldedBeforeComparingAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Below(int value) => value < {|SST1471:-5|};

                public bool NotFound(int value) => value == -1;
            }
            """);

    /// <summary>Verifies a literal that names itself at a declaration is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NamedDeclarationSitesAreCleanAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
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
    [Test]
    public async Task LiteralInsideNonBareInitializerIsReportedAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
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
    [Test]
    public async Task LiteralInsideLambdaUnderReadonlyFieldIsReportedAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
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
    [Test]
    public async Task BitPatternsAndShiftDistancesAreCleanAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
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
    [Test]
    public async Task CardinalityGuardsAreCleanAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
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
    [Test]
    public async Task LabelledAndBufferLengthsAreCleanAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
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
    [Test]
    public async Task GetHashCodeBodyIsCleanAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
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
    [Test]
    public async Task GetHashCodeLocalIsCleanAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
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
    [Test]
    public async Task LocalInsideOrdinaryMethodIsReportedAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
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
    [Test]
    public async Task PositionalConstructorArgumentsAreCleanAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
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
    [Test]
    public async Task DurationFactoryArgumentIsReportedAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public TimeSpan Timeout() => TimeSpan.FromMinutes({|SST1471:30|});
            }
            """);

    /// <summary>Verifies a case label and a constant pattern are reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CaseLabelsAndPatternsAreReportedAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
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
            ("/.editorconfig", """
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
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1471.magic_number_allowed_values = nonsense

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an index into an element access is not reported.</summary>
    /// <remarks>
    /// The literal is a slot, the same positional shape as an array rank, which is already exempt. Naming it
    /// can only produce a constant that restates the number it holds.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElementAccessIndexIsNotReportedAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int[] values) => values[3] + values[7];
            }
            """);

    /// <summary>Verifies the elements of a collection that is a declaration's whole value are not reported.</summary>
    /// <remarks>
    /// <c>var timeout = 500;</c> is exempt because the name explains the number, and this is that statement
    /// three times over — the name explains the whole list. Reporting some of the elements was the odd part:
    /// it pointed at the 2 and the 3 and left the 1 alone, because 1 is allowlisted.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CollectionThatIsAWholeInitializerIsNotReportedAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static readonly int[] Offsets = [0, 4, 8];

                private static readonly int[] Legacy = new[] { 2, 3 };

                public int M() => Offsets.Length + Legacy.Length;
            }
            """);

    /// <summary>Verifies a literal inside a collection passed straight to a call is still reported.</summary>
    /// <remarks>The exemption is the declaration's name explaining the list; an argument has no such name.</remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CollectionPassedAsAnArgumentIsStillReportedAsync()
        => await VerifyMagicNumber.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M() => Sum([{|SST1471:4|}, {|SST1471:8|}]);

                private static int Sum(int[] values) => values.Length;
            }
            """);
}
