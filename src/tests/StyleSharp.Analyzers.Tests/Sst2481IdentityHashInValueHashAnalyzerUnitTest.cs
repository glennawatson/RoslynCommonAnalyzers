// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2481IdentityHashInValueHashAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2481 (the base object identity hash folded into a value hash).</summary>
public class Sst2481IdentityHashInValueHashAnalyzerUnitTest
{
    /// <summary>Verifies an expression-bodied hash that xors in the base object hash is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodyXorOverObjectBaseIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly int _value;

                public override int GetHashCode() => {|SST2481:base.GetHashCode()|} ^ _value.GetHashCode();
            }
            """);

    /// <summary>Verifies a block-bodied hash that returns the base hash mixed with a field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockBodyReturnMixOverObjectBaseIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly int _value;

                public override int GetHashCode()
                {
                    return {|SST2481:base.GetHashCode()|} ^ _value;
                }
            }
            """);

    /// <summary>Verifies the base hash stored in a local and then mixed into the returned hash is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseHashStoredInLocalThenMixedIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly int _value;

                public override int GetHashCode()
                {
                    int seed = {|SST2481:base.GetHashCode()|};
                    return seed ^ _value;
                }
            }
            """);

    /// <summary>Verifies the base hash combined through <c>HashCode.Combine</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HashCodeCombineOverObjectBaseIsReportedAsync()
        => await VerifyNetAsync(
            """
            public class C
            {
                private readonly int _a;
                private readonly int _b;

                public override int GetHashCode() => System.HashCode.Combine({|SST2481:base.GetHashCode()|}, _a, _b);
            }
            """);

    /// <summary>Verifies a base call that binds to a base class's own value hash is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueHashBaseChainIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class B
            {
                private readonly int _y;

                public override bool Equals(object obj) => obj is B b && b._y == _y;

                public override int GetHashCode() => _y.GetHashCode();
            }

            public class D : B
            {
                private readonly int _z;

                public override bool Equals(object obj) => base.Equals(obj) && obj is D d && d._z == _z;

                public override int GetHashCode() => base.GetHashCode() ^ _z.GetHashCode();
            }
            """);

    /// <summary>Verifies a hash that returns the base object hash outright (delegation) is not reported here.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WholeResultDelegationIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public override int GetHashCode() => base.GetHashCode();
            }

            public class D
            {
                public override int GetHashCode()
                {
                    return base.GetHashCode();
                }
            }
            """);

    /// <summary>Verifies a value hash that never calls the base hash is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueHashWithoutBaseCallIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly int _a;
                private readonly int _b;

                public override int GetHashCode() => _a.GetHashCode() ^ _b.GetHashCode();
            }
            """);

    /// <summary>Verifies the base hash folded inside a method that is not the hash override is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseHashInNonHashMethodIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly int _value;

                public int Mix() => base.GetHashCode() ^ _value;
            }
            """);

    /// <summary>Verifies the base hash folded inside a parameterized <c>GetHashCode</c> overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseHashInParameterizedOverloadIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int GetHashCode(int seed) => base.GetHashCode() ^ seed;
            }
            """);

    /// <summary>Verifies the base hash folded inside a shadowing (non-override) <c>GetHashCode</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseHashInShadowingGetHashCodeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly int _value;

                public new int GetHashCode() => base.GetHashCode() ^ _value;
            }
            """);

    /// <summary>Verifies the base hash folded inside a lambda within the hash override is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseHashInsideLambdaIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                private readonly int _value;

                public override int GetHashCode()
                {
                    Func<int> compute = () => base.GetHashCode() ^ _value;
                    return compute();
                }
            }
            """);

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies for framework-dependent shapes.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNetAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
