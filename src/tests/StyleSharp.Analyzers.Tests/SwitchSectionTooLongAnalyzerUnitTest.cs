// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySectionLength = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1524SwitchSectionTooLongAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1524 (switch sections should not be too long).</summary>
public class SwitchSectionTooLongAnalyzerUnitTest
{
    /// <summary>Verifies a section over the default maximum of 20 code lines is reported and a short one is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SectionOverTheDefaultMaximumIsReportedAsync()
    {
        var test = new VerifySectionLength.Test
        {
            TestCode = $$"""
                       public class C
                       {
                           public int Handle(int state)
                           {
                               var total = 0;
                               switch (state)
                               {
                                   case 1:
                       {{BuildStatements(21)}}
                                       return total;

                                   case 2:
                                       return total + 1;

                                   default:
                                       return 0;
                               }
                           }
                       }
                       """,
        };

        // The label, 21 additions, and the return.
        test.ExpectedDiagnostics.Add(VerifySectionLength.Diagnostic().WithSpan(8, 13, 8, 20).WithArguments(23, 20));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies blank lines and comments inside a section do not count toward its length.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLinesAndCommentsDoNotCountAsync()
    {
        var test = new VerifySectionLength.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Handle(int state)
                           {
                               switch (state)
                               {
                                   case 1:
                                       // Why this case matters.
                                       var total = 1;

                                       // And what it does next.
                                       total += 2;

                                       return total;

                                   default:
                                       return 0;
                               }
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig("stylesharp.SST1524.max_switch_section_lines = 4")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a section with several labels is measured from the first of them, which is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SharedLabelsAreMeasuredTogetherAsync()
    {
        var test = new VerifySectionLength.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Handle(int state)
                           {
                               switch (state)
                               {
                                   {|SST1524:case 1:|}
                                   case 2:
                                       var total = 1;
                                       total += 2;
                                       total += 3;
                                       return total;

                                   default:
                                       return 0;
                               }
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig("stylesharp.SST1524.max_switch_section_lines = 5")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a switch expression's arms are not measured: an arm is one expression by construction.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SwitchExpressionArmsAreCleanAsync()
    {
        var test = new VerifySectionLength.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Handle(int state) => state switch
                           {
                               1 => 1 + 1 + 1 + 1,
                               2 => 2 + 2 + 2 + 2,
                               _ => 0,
                           };
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig("stylesharp.SST1524.max_switch_section_lines = 1")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule-specific maximum overrides the project-wide one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleSpecificMaximumWinsOverGeneralAsync()
    {
        var test = new VerifySectionLength.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Handle(int state)
                           {
                               switch (state)
                               {
                                   {|SST1524:case 1:|}
                                       var total = 1;
                                       total += 2;
                                       return total;

                                   default:
                                       return 0;
                               }
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig",
            BuildConfig("stylesharp.max_switch_section_lines = 40", "stylesharp.SST1524.max_switch_section_lines = 3")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide maximum applies when no rule-specific key is set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralMaximumAppliesAsync()
    {
        var test = new VerifySectionLength.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Handle(int state)
                           {
                               switch (state)
                               {
                                   {|SST1524:case 1:|}
                                       var total = 1;
                                       total += 2;
                                       return total;

                                   default:
                                       return 0;
                               }
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig("stylesharp.max_switch_section_lines = 3")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Builds the requested number of accumulator statements at switch-section indentation.</summary>
    /// <param name="count">The number of statements to emit.</param>
    /// <returns>The generated statements, one per line.</returns>
    private static string BuildStatements(int count)
        => string.Join("\n", Enumerable.Range(0, count).Select(static i => $"                total += {i};"));

    /// <summary>Builds an editor config file body from the supplied keys.</summary>
    /// <param name="entries">The keys to write under the C# section.</param>
    /// <returns>The editor config text.</returns>
    private static string BuildConfig(params string[] entries)
        => "root = true\n[*.cs]\n" + string.Join("\n", entries) + "\n";
}
