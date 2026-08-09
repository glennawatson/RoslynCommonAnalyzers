// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRedundantStringToString = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2286RedundantStringToStringAnalyzer,
    StyleSharp.Analyzers.Sst2286RedundantStringToStringCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2286RedundantStringToStringAnalyzer"/> and its code fix (SST2286).</summary>
public class RedundantStringToStringAnalyzerUnitTest
{
    /// <summary>Verifies a ToString call on a string parameter is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToStringOnStringIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public string M(string text) => {|SST2286:text.ToString()|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public string M(string text) => text;
                                   }
                                   """;
        await VerifyRedundantStringToString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a ToString call inside a concatenation is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToStringInConcatenationIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public string M(string text) => "id: " + {|SST2286:text.ToString()|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public string M(string text) => "id: " + text;
                                   }
                                   """;
        await VerifyRedundantStringToString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a ToString call on a parenthesized string expression keeps its parentheses.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToStringOnParenthesizedExpressionIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public int M(string a, string b) => {|SST2286:(a + b).ToString()|}.Length;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int M(string a, string b) => (a + b).Length;
                                   }
                                   """;
        await VerifyRedundantStringToString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a ToString call on a non-string receiver is left alone; it does real work.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToStringOnNonStringIsCleanAsync()
        => await VerifyRedundantStringToString.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public string M(int value) => value.ToString();
            }
            """);

    /// <summary>Verifies a formatting ToString overload is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToStringWithArgumentIsCleanAsync()
        => await VerifyRedundantStringToString.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public string M(string text) => text.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            """);

    /// <summary>Verifies a call in an interpolation hole is left to the interpolation rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToStringInInterpolationIsCleanAsync()
        => await VerifyRedundantStringToString.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public string M(string text) => $"{text.ToString()}";
            }
            """);

    /// <summary>Verifies a conditional-access call is left alone; it is a member binding, not a member access.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalAccessToStringIsCleanAsync()
        => await VerifyRedundantStringToString.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public string M(string text) => text?.ToString();
            }
            """);

    /// <summary>Verifies an unrelated argument-less call is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherArgumentlessCallIsCleanAsync()
        => await VerifyRedundantStringToString.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public string M(string text) => text.Trim();
            }
            """);
}
