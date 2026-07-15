// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2435ValueEqualityFastPathAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2435 (a base value equality used as an equality fast path).</summary>
public class Sst2435ValueEqualityFastPathAnalyzerUnitTest
{
    /// <summary>Verifies an early-return-true fast path over a value-equality base is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EarlyReturnTrueOverValueBaseIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class B
            {
                public int Y { get; set; }

                public override bool Equals(object obj) => obj is B b && b.Y == Y;

                public override int GetHashCode() => Y;
            }

            public class D : B
            {
                public int Z { get; set; }

                public override bool Equals(object obj)
                {
                    if ({|SST2435:base.Equals(obj)|})
                    {
                        return true;
                    }

                    return obj is D d && d.Z == Z;
                }

                public override int GetHashCode() => Z;
            }
            """);

    /// <summary>Verifies a base.Equals(...) || ... short-circuit over a value-equality base is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrShortCircuitOverValueBaseIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class B
            {
                public int Y { get; set; }

                public override bool Equals(object obj) => obj is B b && b.Y == Y;

                public override int GetHashCode() => Y;
            }

            public class D : B
            {
                public int Z { get; set; }

                public override bool Equals(object obj) => {|SST2435:base.Equals(obj)|} || (obj is D d && d.Z == Z);

                public override int GetHashCode() => Z;
            }
            """);

    /// <summary>Verifies the correct base.Equals(...) &amp;&amp; ... shape is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AndCombinationIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class B
            {
                public int Y { get; set; }

                public override bool Equals(object obj) => obj is B b && b.Y == Y;

                public override int GetHashCode() => Y;
            }

            public class D : B
            {
                public int Z { get; set; }

                public override bool Equals(object obj) => base.Equals(obj) && obj is D d && d.Z == Z;

                public override int GetHashCode() => Z;
            }
            """);

    /// <summary>Verifies a guarded fast path against an object base (real reference equality) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedFastPathAgainstObjectBaseIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int X { get; set; }

                public override bool Equals(object obj)
                {
                    if (base.Equals(obj))
                    {
                        return true;
                    }

                    return obj is C c && c.X == X;
                }

                public override int GetHashCode() => X;
            }
            """);
}
