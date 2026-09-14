// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1447BaseObjectEqualityDelegationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1447BaseObjectEqualityDelegationAnalyzer"/> (SST1447 base-object equality delegation).</summary>
public class Sst1447BaseObjectEqualityDelegationAnalyzerUnitTest
{
    /// <summary>Verifies parentheses do not hide a returned identity result.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ParenthesizedReturnedBaseCallsAreReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            class C
            {
                public override bool Equals(object obj) { return (({|SST1447:base.Equals(obj)|})); }
                public override int GetHashCode() => (({|SST1447:base.GetHashCode()|}));
            }
            """);

    /// <summary>Verifies nested functions and non-method members are not mistaken for the outer equality method.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NestedFunctionsAndPropertiesAreCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;
            class C
            {
                int Identity => base.GetHashCode();
                public override bool Equals(object obj)
                {
                    bool Local() => base.Equals(obj);
                    Func<bool> lambda = () => base.Equals(obj);
                    Func<bool> anonymous = delegate { return base.Equals(obj); };
                    return Local() && lambda() && anonymous();
                }
                public override int GetHashCode() => 0;
            }
            """);

    /// <summary>Verifies ordinary calls and differently shaped base overloads stay silent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OtherReceiversNamesAndAritiesAreCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            class B
            {
                protected bool Equals() => true;
                protected int GetHashCode(int value) => value;
            }
            class C : B
            {
                public override bool Equals(object obj)
                {
                    base.ToString();
                    obj.Equals(this);
                    Equals();
                    return base.Equals();
                }
                public override int GetHashCode() => base.GetHashCode(1);
            }
            """);

    /// <summary>Verifies unresolved base calls and malformed top-level calls do not report.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnresolvedAndTopLevelBaseCallsAreCleanAsync() =>
        new Verify.Test
        {
            TestCode = "base.GetHashCode(); class C { public override int GetHashCode() => base.GetHashCode<int>(); }",
            CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.None,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies base.Equals inside Equals is flagged when the base is object.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BaseEqualsBindingToObjectIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public override bool Equals(object obj) => {|SST1447:base.Equals(obj)|};

                public override int GetHashCode() => 42;
            }
            """);

    /// <summary>Verifies base.GetHashCode inside GetHashCode is flagged when the base is object.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BaseGetHashCodeBindingToObjectIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public override bool Equals(object obj) => obj is C;

                public override int GetHashCode() => {|SST1447:base.GetHashCode()|};
            }
            """);

    /// <summary>Verifies a base call that binds to a real base override is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BaseCallBindingToRealOverrideIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BaseCallThroughSilentBaseIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BaseCallOutsideEqualityMembersIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Identity() => base.GetHashCode();
            }
            """);

    /// <summary>Verifies a guarded reference-equality fast path against an object base is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GuardedFastPathAgainstObjectBaseIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
