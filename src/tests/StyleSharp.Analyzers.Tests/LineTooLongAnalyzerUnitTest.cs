// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLineLength = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1521LineTooLongAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1521 (lines should not be too long).</summary>
public class LineTooLongAnalyzerUnitTest
{
    /// <summary>The number of <c>Name</c> terms concatenated into one expression to push its line past the default maximum.</summary>
    private const int OverlongLineTermCount = 25;

    /// <summary>Verifies a line over the default maximum is reported and one inside it is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LineOverTheDefaultMaximumIsReportedAsync()
    {
        const int InsideMaximumLineTermCount = 5;

        var wide = string.Join(" + ", Enumerable.Repeat("Name", OverlongLineTermCount));
        var narrow = string.Join(" + ", Enumerable.Repeat("Name", InsideMaximumLineTermCount));
        var source = $$"""
                     public class C
                     {
                     {|SST1521:    public string Over => {{wide}};|}

                         public string Inside => {{narrow}};

                         public string Name => "n";
                     }
                     """;

        await VerifyLineLength.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies the message reports the measured length and the configured maximum.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MessageReportsTheMeasuredLengthAsync()
    {
        const int ConfiguredMaxLineLength = 40;
        const int MeasuredLineLength = 42;
        const int ReportedLineNumber = 3;
        const int ReportedLineEndColumn = 43;

        var test = new VerifyLineLength.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Value => 1 + 1 + 1 + 1 + 1;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig("stylesharp.SST1521.max_line_length = 40")));
        test.ExpectedDiagnostics.Add(
            VerifyLineLength.Diagnostic()
                .WithSpan(ReportedLineNumber, 1, ReportedLineNumber, ReportedLineEndColumn)
                .WithArguments(MeasuredLineLength, ConfiguredMaxLineLength));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a long line whose overflow is one unbreakable word inside a comment is exempt.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnbreakableWordInACommentIsExemptAsync()
    {
        const int UnbreakableUrlPaddingLength = 120;

        var url = "https://example.invalid/" + new string('x', UnbreakableUrlPaddingLength);
        var source = $$"""
                     public class C
                     {
                         // {{url}}
                         public int Value => 1;
                     }
                     """;

        await VerifyLineLength.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies a long line whose overflow is one unbreakable run inside a string literal is exempt.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnbreakableRunInAStringLiteralIsExemptAsync()
    {
        const int UnbreakableLiteralRunLength = 160;

        var blob = new string('Z', UnbreakableLiteralRunLength);
        var source = $$"""
                     public class C
                     {
                         public string Key => "{{blob}}";
                     }
                     """;

        await VerifyLineLength.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies a long comment made of ordinary words is reported: it can simply be wrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WrappableCommentIsReportedAsync()
    {
        const int WrappableCommentWordCount = 30;

        var prose = string.Join(" ", Enumerable.Repeat("word", WrappableCommentWordCount));
        var source = $$"""
                     public class C
                     {
                     {|SST1521:    // {{prose}}|}
                         public int Value => 1;
                     }
                     """;

        await VerifyLineLength.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies a long unbroken run of code is still reported: nothing broke it, but something could.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnbreakableRunOfCodeIsStillReportedAsync()
    {
        const int SelfAccessChainLinkCount = 30;

        var chain = string.Concat(Enumerable.Repeat(".Self", SelfAccessChainLinkCount));
        var source = $$"""
                     public class C
                     {
                         public C Self => this;

                     {|SST1521:    public C Chain => Self{{chain}};|}
                     }
                     """;

        await VerifyLineLength.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies the rule-specific maximum overrides the project-wide one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleSpecificMaximumWinsOverGeneralAsync()
    {
        var test = new VerifyLineLength.Test
        {
            TestCode = """
                       public class C
                       {
                       {|SST1521:    public int Value => 1 + 1 + 1 + 1 + 1;|}

                           public int Small => 1;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", BuildConfig("stylesharp.max_line_length = 200", "stylesharp.SST1521.max_line_length = 40")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide maximum applies when no rule-specific key is set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralMaximumAppliesAsync()
    {
        var test = new VerifyLineLength.Test
        {
            TestCode = """
                       public class C
                       {
                       {|SST1521:    public int Value => 1 + 1 + 1 + 1 + 1;|}
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig("stylesharp.max_line_length = 40")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an unparsable maximum falls back to the default rather than disabling the rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnparsableMaximumFallsBackToTheDefaultAsync()
    {
        var wide = string.Join(" + ", Enumerable.Repeat("Name", OverlongLineTermCount));
        var test = new VerifyLineLength.Test
        {
            TestCode = $$"""
                       public class C
                       {
                       {|SST1521:    public string Over => {{wide}};|}

                           public string Name => "n";
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig("stylesharp.SST1521.max_line_length = wide")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Builds an editor config file body from the supplied keys.</summary>
    /// <param name="entries">The keys to write under the C# section.</param>
    /// <returns>The editor config text.</returns>
    private static string BuildConfig(params string[] entries)
        => "root = true\n[*.cs]\n" + string.Join("\n", entries) + "\n";
}
