// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRedundantCast = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExpressionSimplificationAnalyzer,
    StyleSharp.Analyzers.RedundantCastCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1175 (unnecessary casts) and its fix.</summary>
public class RedundantCastAnalyzerUnitTest
{
    /// <summary>Verifies a cast to the operand's own type is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdentityCastRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(int x) => ({|SST1175:int|})x;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(int x) => x;
                                   }
                                   """;
        await VerifyRedundantCast.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a cast that widens to a different type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WideningCastIsCleanAsync()
        => await VerifyRedundantCast.VerifyAnalyzerAsync(
            """
            public class C
            {
                public long M(int x) => (long)x;

                public object Boxed(int x) => (object)x;
            }
            """);
}
