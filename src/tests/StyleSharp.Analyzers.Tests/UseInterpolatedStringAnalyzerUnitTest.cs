// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyUseInterpolatedString = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2249UseInterpolatedStringAnalyzer,
    StyleSharp.Analyzers.Sst2249UseInterpolatedStringCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2249UseInterpolatedStringAnalyzer"/> and its code fix (SST2249).</summary>
public class UseInterpolatedStringAnalyzerUnitTest
{
    /// <summary>Minimal stubs for the logging-extensions surface whose message template must stay non-interpolated.</summary>
    private const string LoggingStubs = """
        using Microsoft.Extensions.Logging;

        namespace Microsoft.Extensions.Logging
        {
            public interface ILogger { }
            public static class LoggerExtensions
            {
                public static void LogInformation(this ILogger logger, string message, params object[] args) { }
            }
        }
        """;

    /// <summary>Verifies a two-placeholder composite format call is reported and interpolated.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompositeFormatIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(string a, string b) => {|SST2249:string.Format("{0} {1}", a, b)|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(string a, string b) => $"{a} {b}";
                                   }
                                   """;
        await VerifyUseInterpolatedString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies alignment and a format specifier are reproduced in the interpolation hole.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompositeFormatKeepsAlignmentAndSpecifierAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(int value) => {|SST2249:string.Format("{0,5:X}", value)|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(int value) => $"{value,5:X}";
                                   }
                                   """;
        await VerifyUseInterpolatedString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a reordered placeholder set maps each hole to the right value.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompositeFormatReordersPlaceholdersAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(string a, string b) => {|SST2249:string.Format("{1}-{0}", a, b)|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(string a, string b) => $"{b}-{a}";
                                   }
                                   """;
        await VerifyUseInterpolatedString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies escaped braces in a format string are preserved as literal braces.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompositeFormatKeepsEscapedBracesAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(string a) => {|SST2249:string.Format("{{{0}}}", a)|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(string a) => $"{{{a}}}";
                                   }
                                   """;
        await VerifyUseInterpolatedString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a conditional value is parenthesized so its colon does not read as a format separator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompositeFormatParenthesizesConditionalValueAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(bool flag) => {|SST2249:string.Format("{0}", flag ? "y" : "n")|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(bool flag) => $"{(flag ? "y" : "n")}";
                                   }
                                   """;
        await VerifyUseInterpolatedString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a literal-plus-value concatenation is reported and interpolated.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenationIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(string name) => {|SST2249:"Hello " + name + "!"|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(string name) => $"Hello {name}!";
                                   }
                                   """;
        await VerifyUseInterpolatedString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a non-string leading operand still concatenates once a string literal leads the chain.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenationWithLeadingLiteralAndNumberIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(int count) => {|SST2249:"count: " + count|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(int count) => $"count: {count}";
                                   }
                                   """;
        await VerifyUseInterpolatedString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies literal braces in a concatenation literal are doubled in the interpolated string.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenationEscapesLiteralBracesAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(string value) => {|SST2249:"a{b}c" + value|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(string value) => $"a{{b}}c{value}";
                                   }
                                   """;
        await VerifyUseInterpolatedString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a composite format that passes an explicit provider is left alone so its culture is not dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitProviderFormatIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            using System.Globalization;

            public sealed class C
            {
                public string M(string a, string b) => string.Format(CultureInfo.InvariantCulture, "{0} {1}", a, b);
            }
            """);

    /// <summary>Verifies a format whose format string is not a literal is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLiteralFormatIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string format, string a) => string.Format(format, a);
            }
            """);

    /// <summary>Verifies a concatenation used as a structured-logging message template is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenationInLoggingTemplateIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            LoggingStubs + """

            public sealed class C
            {
                public void M(ILogger logger, string name) => logger.LogInformation("User " + name + " signed in");
            }
            """);

    /// <summary>Verifies a composite format used as a structured-logging message template is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompositeFormatInLoggingTemplateIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            LoggingStubs + """

            public sealed class C
            {
                public void M(ILogger logger, string name) => logger.LogInformation(string.Format("User {0}", name));
            }
            """);

    /// <summary>Verifies a concatenation passed among a log call's format arguments — not the template — is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenationInLoggingValueArgumentIsReportedAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            LoggingStubs + """

            public sealed class C
            {
                public void M(ILogger logger, string name) => logger.LogInformation("User {Name}", {|SST2249:"id-" + name|});
            }
            """);

    /// <summary>Verifies a concatenation argument to a non-logging call is still reported when logging is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenationInNonLoggingCallIsReportedAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            LoggingStubs + """

            public sealed class C
            {
                public string Wrap(string value) => value;
                public void M(ILogger logger, string name) => Wrap({|SST2249:"User " + name|});
            }
            """);

    /// <summary>Verifies a chain whose leading operands add as numbers is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NumericFoldConcatenationIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M() => 1 + 2 + " items";
            }
            """);

    /// <summary>Verifies a concatenation with no string literal is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenationWithoutLiteralIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string first, string last) => first + last;
            }
            """);

    /// <summary>Verifies a lone array handed to the params parameter is left alone, because it is spread not printed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleArrayArgumentIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(object[] values) => string.Format("{0} {1}", values);
            }
            """);

    /// <summary>Verifies a repeated placeholder index is left alone, so a value is never evaluated twice.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RepeatedPlaceholderIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string a) => string.Format("{0} {0}", a);
            }
            """);

    /// <summary>Verifies a placeholder set with a gap is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GappedPlaceholderIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string a, string b, string c) => string.Format("{0} {2}", a, b, c);
            }
            """);

    /// <summary>Verifies a call to a same-named method that is not string.Format is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForeignFormatMethodIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private static string Format(string a, string b) => a + b;

                public string M(string a, string b) => Format(a, b);
            }
            """);

    /// <summary>Verifies escape sequences and a control character are reproduced in the interpolated string.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenationEscapesControlCharactersAsync()
    {
        const string Source = """"
                              public sealed class C
                              {
                                  public string M(string value) => {|SST2249:"x\t\r\n\\\"\a" + value|};
                              }
                              """";
        const string FixedSource = """"
                                   public sealed class C
                                   {
                                       public string M(string value) => $"x\t\r\n\\\"\u0007{value}";
                                   }
                                   """";
        await VerifyUseInterpolatedString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a negative alignment is reproduced in the interpolation hole.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompositeFormatKeepsNegativeAlignmentAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(int value) => {|SST2249:string.Format("{0,-5}", value)|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(int value) => $"{value,-5}";
                                   }
                                   """;
        await VerifyUseInterpolatedString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a fully qualified <c>System.String.Format</c> receiver is reported and interpolated.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedStringReceiverIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(string a, string b) => {|SST2249:System.String.Format("{0} {1}", a, b)|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(string a, string b) => $"{a} {b}";
                                   }
                                   """;
        await VerifyUseInterpolatedString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a verbatim format string is left alone, keeping the escaping simple.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VerbatimFormatStringIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string a, string b) => string.Format(@"{0} {1}", a, b);
            }
            """);

    /// <summary>Verifies a verbatim string operand in a concatenation is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VerbatimConcatenationOperandIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string value) => @"a{b}" + value;
            }
            """);

    /// <summary>Verifies a call with named arguments is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedArgumentFormatIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string a) => string.Format(format: "{0}", arg0: a);
            }
            """);

    /// <summary>Verifies an unqualified <c>String.Format</c> receiver is reported and interpolated.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringIdentifierReceiverIsFixedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public string M(string a, string b) => {|SST2249:String.Format("{0} {1}", a, b)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public string M(string a, string b) => $"{a} {b}";
                                   }
                                   """;
        await VerifyUseInterpolatedString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies whitespace around an alignment is dropped when the hole is built.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompositeFormatNormalizesSpacedAlignmentAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(int value) => {|SST2249:string.Format("{0, 5}", value)|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(int value) => $"{value,5}";
                                   }
                                   """;
        await VerifyUseInterpolatedString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a <c>Format</c> call on a non-string receiver is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonStringReceiverFormatIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(Widget w, string a) => w.Format("{0}", a);
            }

            public sealed class Widget
            {
                public string Format(string format, string value) => format + value;
            }
            """);

    /// <summary>Verifies a format string with an unescaped closing brace is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoneClosingBraceFormatIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string a) => string.Format("}{0}", a);
            }
            """);

    /// <summary>Verifies an unterminated placeholder is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnterminatedPlaceholderFormatIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string a) => string.Format("{0", a);
            }
            """);

    /// <summary>Verifies an alignment clause with no digits is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyAlignmentFormatIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string a) => string.Format("{0,}", a);
            }
            """);

    /// <summary>Verifies an unterminated format specifier is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnterminatedSpecifierFormatIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(int value) => string.Format("{0:X", value);
            }
            """);

    /// <summary>Verifies a format specifier that opens a brace is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BraceInSpecifierFormatIsSilentAsync()
        => await VerifyUseInterpolatedString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(int value) => string.Format("{0:{}", value);
            }
            """);

    /// <summary>Verifies the rule stays silent below C# 6, where interpolated strings do not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp6Async()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(string a, string b)
                                  {
                                      return string.Format("{0} {1}", a, b);
                                  }
                              }
                              """;
        var test = new VerifyUseInterpolatedString.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp5));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
