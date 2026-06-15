// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUseStringEmpty = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1122UseStringEmptyAnalyzer,
    StyleSharp.Analyzers.Sst1122UseStringEmptyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the use-string.Empty rule (SST1122).</summary>
public class UseStringEmptyAnalyzerUnitTest
{
    /// <summary>Verifies an empty string literal is reported (SST1122) and replaced with string.Empty.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyStringReplacedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private string M() => {|SST1122:""|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private string M() => string.Empty;
                                   }
                                   """;
        await VerifyUseStringEmpty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All replaces every empty string literal in a document in a single pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private string First() => {|SST1122:""|};

                                  private string Second() => {|SST1122:""|};

                                  private string Third() => {|SST1122:""|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private string First() => string.Empty;

                                       private string Second() => string.Empty;

                                       private string Third() => string.Empty;
                                   }
                                   """;
        await VerifyUseStringEmpty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a non-empty string literal is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonEmptyStringIsCleanAsync()
        => await VerifyUseStringEmpty.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private string M() => "value";
            }
            """);

    /// <summary>Verifies empty strings in constant-required contexts are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantContextsAreCleanAsync()
        => await VerifyUseStringEmpty.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                private const string Constant = "";

                [Obsolete("")]
                private static void M(string value = "")
                {
                    const string local = "";
                    switch (value)
                    {
                        case "":
                            break;
                    }

                    _ = local + Constant;
                }
            }
            """);
}
