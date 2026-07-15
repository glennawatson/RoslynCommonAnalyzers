// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2318DuplicateMemberBodyAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2318 (two methods in one type with token-identical bodies).</summary>
public class Sst2318DuplicateMemberBodyAnalyzerUnitTest
{
    /// <summary>Verifies the second of two methods with identical multi-statement bodies is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdenticalMultiStatementBodiesReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int First(int a, int b)
                {
                    var x = a + b;
                    return x * 2;
                }

                public int {|SST2318:Second|}(int a, int b)
                {
                    var x = a + b;
                    return x * 2;
                }
            }
            """);

    /// <summary>Verifies two identical non-trivial expression bodies are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdenticalExpressionBodiesReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int First(int a, int b) => (a + b) * 2;

                public int {|SST2318:Second|}(int a, int b) => (a + b) * 2;
            }
            """);

    /// <summary>Verifies two methods whose bodies differ are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferingBodiesAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int First(int a, int b)
                {
                    var x = a + b;
                    return x * 2;
                }

                public int Second(int a, int b)
                {
                    var x = a - b;
                    return x * 2;
                }
            }
            """);

    /// <summary>Verifies two trivial expression-bodied methods are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrivialBodiesAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int A() => 0;

                public int B() => 0;
            }
            """);

    /// <summary>Verifies identical bodies in different types are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdenticalBodiesInDifferentTypesAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class First
            {
                public int M(int a, int b)
                {
                    var x = a + b;
                    return x * 2;
                }
            }

            public sealed class Second
            {
                public int M(int a, int b)
                {
                    var x = a + b;
                    return x * 2;
                }
            }
            """);

    /// <summary>Verifies abstract methods, which have no body, are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractMethodsAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class C
            {
                public abstract int A();

                public abstract int B();
            }
            """);

    /// <summary>Verifies a single-throw body counts as trivial and is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleThrowBodiesAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int A()
                {
                    throw new System.NotImplementedException();
                }

                public int B()
                {
                    throw new System.NotImplementedException();
                }
            }
            """);
}
