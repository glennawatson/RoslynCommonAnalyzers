// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDuplicatedString = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1486DuplicatedStringLiteralAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1486 (repeated string literals should be named constants).</summary>
public class DuplicatedStringLiteralAnalyzerUnitTest
{
    /// <summary>Verifies three copies of one literal are reported once, on the first copy.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ThreeCopiesAreReportedOnTheFirstCopyAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string First() => {|SST1486:"repeated-value"|};

                public string Second() => "repeated-value";

                public string Third() => "repeated-value";
            }
            """);

    /// <summary>Verifies two copies stay below the default threshold.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TwoCopiesAreCleanAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string First() => "repeated-value";

                public string Second() => "repeated-value";
            }
            """);

    /// <summary>Verifies distinct literals are never merged, however often each is written.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DistinctLiteralsAreCleanAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string First() => "alpha-value";

                public string Second() => "beta-value";

                public string Third() => "gamma-value";

                public string Fourth() => "delta-value";
            }
            """);

    /// <summary>Verifies a literal shorter than the minimum length buys nothing by being named.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ShortLiteralsAreCleanAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string First() => "abc";

                public string Second() => "abc";

                public string Third() => "abc";

                public string Fourth() => ",";

                public string Fifth() => ",";

                public string Sixth() => ",";
            }
            """);

    /// <summary>Verifies the empty string and a whitespace-only literal are never reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The whitespace-only literal is long enough to pass the minimum length, so only the value can exclude it.</remarks>
    [Test]
    public async Task EmptyAndWhitespaceLiteralsAreCleanAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string First() => "";

                public string Second() => "";

                public string Third() => "";

                public string Fourth() => "      ";

                public string Fifth() => "      ";

                public string Sixth() => "      ";
            }
            """);

    /// <summary>Verifies a repeated attribute argument is idiomatic rather than a duplicate.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AttributeArgumentsAreCleanAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                [Obsolete("use the new overload")]
                public string First() => "alpha-value";

                [Obsolete("use the new overload")]
                public string Second() => "beta-value";

                [Obsolete("use the new overload")]
                public string Third() => "gamma-value";
            }
            """);

    /// <summary>Verifies a constant declaration is the named constant, so it is never asked to be named again.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstantAndStaticReadonlyFieldsAreCleanAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            public class C
            {
                private const string Primary = "shared-key-value";

                private static readonly string Alias = "shared-key-value";

                private const string Backup = "shared-key-value";

                public string All() => Primary + Alias + Backup;
            }
            """);

    /// <summary>Verifies a <c>const</c> local is the named constant too.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstantLocalsAreCleanAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string First()
                {
                    const string Key = "local-key-value";
                    return Key;
                }

                public string Second()
                {
                    const string Key = "local-key-value";
                    return Key;
                }

                public string Third()
                {
                    const string Key = "local-key-value";
                    return Key;
                }
            }
            """);

    /// <summary>Verifies a plain field initializer is still reported, because nothing has named it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PlainFieldInitializersAreReportedAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            public class C
            {
                private string _first = {|SST1486:"shared-value"|};

                private string _second = "shared-value";

                private string _third = "shared-value";

                public string All() => _first + _second + _third;
            }
            """);

    /// <summary>Verifies a switch already names its cases structurally.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CaseLabelsAndArmPatternsAreCleanAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Rank(string value)
                {
                    switch (value)
                    {
                        case "alpha-value":
                            return 1;
                        default:
                            return 0;
                    }
                }

                public int Score(string value) => value switch
                {
                    "alpha-value" => 2,
                    _ => 0,
                };

                public int Weight(string value) => value switch
                {
                    "alpha-value" => 3,
                    _ => 0,
                };
            }
            """);

    /// <summary>Verifies the value an arm produces is not a case label, so a repeated one is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SwitchArmValuesAreReportedAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string Name(int value) => value switch
                {
                    1 => {|SST1486:"result-value"|},
                    2 => "result-value",
                    _ => "result-value",
                };
            }
            """);

    /// <summary>Verifies an interpolated string's text segments are a template, not a repeated value.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InterpolatedStringTextIsCleanAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string First(int value) => $"prefix-value {value}";

                public string Second(int value) => $"prefix-value {value}";

                public string Third(int value) => $"prefix-value {value}";
            }
            """);

    /// <summary>Verifies a literal written inside an interpolation hole is an ordinary literal.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LiteralInsideInterpolationHoleIsReportedAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string First() => $"{Get({|SST1486:"lookup-key"|})}";

                public string Second() => $"{Get("lookup-key")}";

                public string Third() => $"{Get("lookup-key")}";

                private static string Get(string key) => key;
            }
            """);

    /// <summary>Verifies a literal in a lambda does not inherit the enclosing field's name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LiteralsInsideLambdasUnderConstantFieldsAreReportedAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                private static readonly Func<string> First = () => {|SST1486:"lambda-value"|};

                private static readonly Func<string> Second = () => "lambda-value";

                private static readonly Func<string> Third = () => "lambda-value";

                public string Run() => First() + Second() + Third();
            }
            """);

    /// <summary>Verifies a verbatim spelling of the same value counts as the same value.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task VerbatimSpellingOfTheSameValueCountsAsync()
        => await VerifyDuplicatedString.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string First() => {|SST1486:"config/settings"|};

                public string Second() => @"config/settings";

                public string Third() => "config/settings";
            }
            """);

    /// <summary>Verifies duplicates are counted per file, not per compilation.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LiteralsInDifferentFilesAreNotAggregatedAsync()
    {
        const string First = """
                             public class A
                             {
                                 public string One() => "shared-across-files";

                                 public string Two() => "shared-across-files";
                             }
                             """;

        const string Second = """
                              public class B
                              {
                                  public string One() => "shared-across-files";
                              }
                              """;

        var test = new VerifyDuplicatedString.Test();
        test.TestState.Sources.Add(("/First.cs", First));
        test.TestState.Sources.Add(("/Second.cs", Second));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the threshold is configurable through .editorconfig.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConfiguredThresholdReportsTwoCopiesAsync()
    {
        var test = new VerifyDuplicatedString.Test
        {
            TestCode = """
                       public class C
                       {
                           public string First() => {|SST1486:"paired-value"|};

                           public string Second() => "paired-value";
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1486.duplicate_string_threshold = 2

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the minimum length is configurable through .editorconfig.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConfiguredMinimumLengthReportsShorterLiteralsAsync()
    {
        var test = new VerifyDuplicatedString.Test
        {
            TestCode = """
                       public class C
                       {
                           public string First() => {|SST1486:"abc"|};

                           public string Second() => "abc";

                           public string Third() => "abc";

                           public string Fourth() => ",";

                           public string Fifth() => ",";

                           public string Sixth() => ",";
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1486.minimum_string_length = 3

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide keys are honoured when no rule-specific key is set.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ProjectWideKeysAreHonouredAsync()
    {
        var test = new VerifyDuplicatedString.Test
        {
            TestCode = """
                       public class C
                       {
                           public string First() => {|SST1486:"pair"|};

                           public string Second() => "pair";
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.duplicate_string_threshold = 2
            stylesharp.minimum_string_length = 4

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a nonsensical threshold falls back to the default instead of reporting every literal.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnusableOptionsFallBackToTheDefaultsAsync()
    {
        var test = new VerifyDuplicatedString.Test
        {
            TestCode = """
                       public class C
                       {
                           public string First() => "single-value";

                           public string Second() => {|SST1486:"repeated-value"|};

                           public string Third() => "repeated-value";

                           public string Fourth() => "repeated-value";
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1486.duplicate_string_threshold = 1
            stylesharp.SST1486.minimum_string_length = nonsense

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
