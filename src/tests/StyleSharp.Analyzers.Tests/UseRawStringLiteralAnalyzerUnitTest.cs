// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRawString = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2243UseRawStringLiteralAnalyzer,
    StyleSharp.Analyzers.Sst2243UseRawStringLiteralCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2243UseRawStringLiteralAnalyzer"/> and its code fix (SST2243).</summary>
public class UseRawStringLiteralAnalyzerUnitTest
{
    /// <summary>Verifies a single-line verbatim literal with doubled-quote escapes is reported and rewritten with a three-quote delimiter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineEscapedQuotesRewrittenAsync()
    {
        const string Source = """"
                              internal class C
                              {
                                  private string _value = {|SST2243:@"say ""hi"" now"|};
                              }
                              """";
        const string FixedSource = """"
                                   internal class C
                                   {
                                       private string _value = """say "hi" now""";
                                   }
                                   """";
        await VerifyRawString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a value with a two-quote run still fits inside the minimum three-quote delimiter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoQuoteRunKeepsMinimumDelimiterAsync()
    {
        const string Source = """""
                              internal class C
                              {
                                  private string _value = {|SST2243:@"x""""y"|};
                              }
                              """"";
        const string FixedSource = """"
                                   internal class C
                                   {
                                       private string _value = """x""y""";
                                   }
                                   """";
        await VerifyRawString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a value with a three-quote run grows the delimiter to four quotes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreeQuoteRunGrowsDelimiterAsync()
    {
        const string Source = """""""
                              internal class C
                              {
                                  private string _value = {|SST2243:@"a""""""b"|};
                              }
                              """"""";
        const string FixedSource = """""
                                   internal class C
                                   {
                                       private string _value = """"a"""b"""";
                                   }
                                   """"";
        await VerifyRawString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a multi-line verbatim literal is reported and rewritten with hanging content indented to the literal's start line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiLineRewrittenAsync()
    {
        const string Source = """"
                              internal class C
                              {
                                  private string _value = {|SST2243:@"first
                              second
                                  indented"|};
                              }
                              """";
        const string FixedSource = """"
                                   internal class C
                                   {
                                       private string _value = """
                                       first
                                       second
                                           indented
                                       """;
                                   }
                                   """";
        await VerifyRawString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an empty value line inside a multi-line verbatim literal stays an empty line after the rewrite.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiLineWithEmptyLineRewrittenAsync()
    {
        const string Source = """"
                              internal class C
                              {
                                  private string _value = {|SST2243:@"alpha

                              omega"|};
                              }
                              """";
        const string FixedSource = """"
                                   internal class C
                                   {
                                       private string _value = """
                                       alpha

                                       omega
                                       """;
                                   }
                                   """";
        await VerifyRawString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every reported literal in a document in a single pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """"
                              internal class C
                              {
                                  private string _first = {|SST2243:@"one ""a"" one"|};
                                  private string _second = {|SST2243:@"two ""b"" two"|};
                              }
                              """";
        const string FixedSource = """"
                                   internal class C
                                   {
                                       private string _first = """one "a" one""";
                                       private string _second = """two "b" two""";
                                   }
                                   """";
        await VerifyRawString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a single-line verbatim literal without doubled-quote escapes is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainVerbatimIsCleanAsync()
        => await VerifyRawString.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private string _value = @"plain text";
            }
            """);

    /// <summary>Verifies a regular literal with backslash escapes is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RegularEscapedLiteralIsCleanAsync()
        => await VerifyRawString.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private string _value = "line one\nline two";
            }
            """);

    /// <summary>Verifies an interpolated verbatim literal is clean even when it carries doubled-quote escapes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterpolatedVerbatimIsCleanAsync()
        => await VerifyRawString.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private string _value = $@"say ""hi"" {0} now";
            }
            """);

    /// <summary>Verifies a single-line verbatim literal whose value starts with a quote is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingQuoteValueIsCleanAsync()
        => await VerifyRawString.VerifyAnalyzerAsync(
            """"
            internal class C
            {
                private string _value = @"""leading";
            }
            """");

    /// <summary>Verifies a single-line verbatim literal whose value ends with a quote is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingQuoteValueIsCleanAsync()
        => await VerifyRawString.VerifyAnalyzerAsync(
            """"
            internal class C
            {
                private string _value = @"trailing""";
            }
            """");

    /// <summary>
    /// Verifies a multi-line verbatim literal with a whitespace-only value line is clean — the raw
    /// string conversion would normalize that line to empty and change the value.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WhitespaceOnlyValueLineIsCleanAsync()
        => await VerifyRawString.VerifyAnalyzerAsync(
            "internal class C\n{\n    private string _value = @\"first\n   \nlast\";\n}\n");
}
