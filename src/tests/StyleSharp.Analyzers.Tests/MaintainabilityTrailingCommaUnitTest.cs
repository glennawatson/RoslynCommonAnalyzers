// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyTrailingComma = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1413TrailingCommaAnalyzer,
    StyleSharp.Analyzers.Sst1413TrailingCommaCodeFixProvider>;

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

    /// <summary>Verifies Fix All adds a trailing comma to every multi-line initializer (SST1413) in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private static readonly int[] Values = new[]
                                  {
                                      1,
                                      {|SST1413:2|}
                                  };

                                  private static readonly int[] More = new[]
                                  {
                                      3,
                                      {|SST1413:4|}
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

                                       private static readonly int[] More = new[]
                                       {
                                           3,
                                           4,
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
