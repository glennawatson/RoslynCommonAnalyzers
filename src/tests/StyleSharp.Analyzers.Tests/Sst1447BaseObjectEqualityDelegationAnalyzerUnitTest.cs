// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1447BaseObjectEqualityDelegationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1447BaseObjectEqualityDelegationAnalyzer"/> (SST1447 base-object equality delegation).</summary>
public class Sst1447BaseObjectEqualityDelegationAnalyzerUnitTest
{
    /// <summary>Verifies base.Equals inside Equals is flagged when the base is object.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BaseEqualsBindingToObjectIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public override bool Equals(object obj) => {|SST1447:base.Equals(obj)|};

                public override int GetHashCode() => 42;
            }
            """);

    /// <summary>Verifies base.GetHashCode inside GetHashCode is flagged when the base is object.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BaseGetHashCodeBindingToObjectIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public override bool Equals(object obj) => obj is C;

                public override int GetHashCode() => {|SST1447:base.GetHashCode()|};
            }
            """);

    /// <summary>Verifies a base call that binds to a real base override is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BaseCallBindingToRealOverrideIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class B
            {
                public override bool Equals(object obj) => obj is B;

                public override int GetHashCode() => 7;
            }

            public class C : B
            {
                public override bool Equals(object obj) => base.Equals(obj) && obj is C;

                public override int GetHashCode() => base.GetHashCode();
            }
            """);

    /// <summary>Verifies a base call binding to object is flagged even with an intermediate base.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BaseCallThroughSilentBaseIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class B
            {
            }

            public class C : B
            {
                public override bool Equals(object obj) => {|SST1447:base.Equals(obj)|};

                public override int GetHashCode() => 42;
            }
            """);

    /// <summary>Verifies base calls outside equality members are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BaseCallOutsideEqualityMembersIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Identity() => base.GetHashCode();
            }
            """);

    /// <summary>Verifies a guarded reference-equality fast path against an object base is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
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
