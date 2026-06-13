// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyInterpolated = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExpressionSimplificationAnalyzer,
    StyleSharp.Analyzers.RedundantInterpolatedStringCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1183 (interpolated string without interpolations) and its fix.</summary>
public class RedundantInterpolatedStringAnalyzerUnitTest
{
    /// <summary>Verifies an interpolation-free interpolated string is reported and the prefix removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoInterpolationsBecomesPlainAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M() => {|SST1183:$"hello"|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M() => "hello";
                                   }
                                   """;
        await VerifyInterpolated.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an interpolated string with a real hole is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WithInterpolationIsCleanAsync()
        => await VerifyInterpolated.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string M(int value) => $"value is {value}";
            }
            """);
}
