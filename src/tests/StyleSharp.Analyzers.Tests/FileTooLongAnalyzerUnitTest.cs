// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFileLength = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1522FileTooLongAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1522 (files should not be too long).</summary>
public class FileTooLongAnalyzerUnitTest
{
    /// <summary>Verifies a file over the default maximum of 500 code lines is reported at its first token.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileOverTheDefaultMaximumIsReportedAsync()
    {
        var test = new VerifyFileLength.Test
        {
            TestCode = BuildClass(500),
        };

        // The declaration, its opening brace, 500 properties, and the closing brace.
        test.ExpectedDiagnostics.Add(VerifyFileLength.Diagnostic().WithSpan(1, 1, 1, 7).WithArguments(503, 500));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a file whose blank lines push it past the maximum, but whose code does not, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The generated file has 801 raw lines and 403 code lines, so only the counted lines can save it.</remarks>
    [Test]
    public async Task FileInsideTheDefaultMaximumIsCleanAsync()
        => await VerifyFileLength.VerifyAnalyzerAsync(BuildClass(400));

    /// <summary>Verifies blank lines and comments do not count toward the limit.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLinesAndCommentsDoNotCountAsync()
    {
        var padding = string.Join("\n", Enumerable.Repeat("// filler\n", 20));
        var test = new VerifyFileLength.Test
        {
            TestCode = $$"""
                       public class C
                       {
                       {{padding}}
                           public int A { get; set; }

                           public int B { get; set; }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig("stylesharp.SST1522.max_file_lines = 10")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule-specific maximum overrides the project-wide one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleSpecificMaximumWinsOverGeneralAsync()
    {
        var test = new VerifyFileLength.Test
        {
            TestCode = """
                       {|SST1522:public|} class C
                       {
                           public int A { get; set; }

                           public int B { get; set; }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", BuildConfig("stylesharp.max_file_lines = 400", "stylesharp.SST1522.max_file_lines = 3")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide maximum applies when no rule-specific key is set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralMaximumAppliesAsync()
    {
        var test = new VerifyFileLength.Test
        {
            TestCode = """
                       {|SST1522:public|} class C
                       {
                           public int A { get; set; }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig("stylesharp.max_file_lines = 3")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Builds a class of single-line properties separated by blank lines.</summary>
    /// <param name="members">The number of properties to emit.</param>
    /// <returns>The generated source, whose code lines are <paramref name="members"/> plus three.</returns>
    private static string BuildClass(int members)
    {
        var body = string.Join("\n\n", Enumerable.Range(0, members).Select(static i => $"    public int P{i} {{ get; set; }}"));
        return $"public class C\n{{\n{body}\n}}\n";
    }

    /// <summary>Builds an editor config file body from the supplied keys.</summary>
    /// <param name="entries">The keys to write under the C# section.</param>
    /// <returns>The editor config text.</returns>
    private static string BuildConfig(params string[] entries)
        => "root = true\n[*.cs]\n" + string.Join("\n", entries) + "\n";
}
