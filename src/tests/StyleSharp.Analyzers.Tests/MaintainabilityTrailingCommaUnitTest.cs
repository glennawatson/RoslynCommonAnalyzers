// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyTrailingComma = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.TrailingCommaAnalyzer,
    StyleSharp.Analyzers.TrailingCommaCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the trailing-comma rule (SST1413).</summary>
public class MaintainabilityTrailingCommaUnitTest
{
    /// <summary>Verifies a multi-line initializer without a trailing comma is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingTrailingCommaAddedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private static readonly int[] Values = new[]
                                  {
                                      1,
                                      {|SST1413:2|}
                                  };
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private static readonly int[] Values = new[]
                                       {
                                           1,
                                           2,
                                       };
                                   }
                                   """;
        await VerifyTrailingComma.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a multi-line initializer that already has a trailing comma is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingCommaPresentIsCleanAsync()
        => await VerifyTrailingComma.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static readonly int[] Values = new[]
                {
                    1,
                    2,
                };
            }
            """);

    /// <summary>Verifies a single-line initializer is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineInitializerIsCleanAsync()
        => await VerifyTrailingComma.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static readonly int[] Values = new[] { 1, 2 };
            }
            """);
}
