// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLambdaBody = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2257SimplifyLambdaBodyAnalyzer,
    StyleSharp.Analyzers.Sst2257SimplifyLambdaBodyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2257SimplifyLambdaBodyAnalyzer"/> and its code fix (SST2257).</summary>
public class SimplifyLambdaBodyAnalyzerUnitTest
{
    /// <summary>Verifies a simple lambda whose block returns one expression is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SimpleLambdaIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  public Func<int, int> M() => x {|SST2257:=>|} { return x + 1; };
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       public Func<int, int> M() => x => x + 1;
                                   }
                                   """;
        await VerifyLambdaBody.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a parenthesized lambda whose block returns one expression is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedLambdaIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  public Func<int, int, int> M() => (a, b) {|SST2257:=>|} { return a + b; };
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       public Func<int, int, int> M() => (a, b) => a + b;
                                   }
                                   """;
        await VerifyLambdaBody.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a lambda that already has an expression body is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedLambdaIsCleanAsync()
        => await VerifyLambdaBody.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                public Func<int, int> M() => x => x + 1;
            }
            """);

    /// <summary>Verifies a lambda whose block has more than one statement is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiStatementBlockIsCleanAsync()
        => await VerifyLambdaBody.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                public Func<int, int> M() => x =>
                {
                    var y = x + 1;
                    return y;
                };
            }
            """);

    /// <summary>Verifies a void lambda whose block is a single expression statement is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VoidExpressionStatementBlockIsCleanAsync()
        => await VerifyLambdaBody.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                public Action<int> M() => x => { System.Console.WriteLine(x); };
            }
            """);
}
