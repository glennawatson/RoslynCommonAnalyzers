// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySwitch = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.TooManySwitchLabelsAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1423 (limit switch statement sections).</summary>
public class TooManySwitchLabelsAnalyzerUnitTest
{
    /// <summary>Verifies the configured rule-specific threshold is respected.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RuleSpecificThresholdIsRespectedAsync()
    {
        var test = new VerifySwitch.Test
        {
            TestCode = """
                       public class C
                       {
                           public void M(int value)
                           {
                               {|SST1423:switch|} (value)
                               {
                                   case 0:
                                       break;
                                   case 1:
                                       break;
                                   case 2:
                                       break;
                               }
                           }
                       }
                       """
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", "root = true\n[*.cs]\nstylesharp.SST1423.max_switch_sections = 2\n"));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies stacked labels count as one section and an at-limit switch is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AtLimitAndStackedLabelsAreCleanAsync()
    {
        var test = new VerifySwitch.Test
        {
            TestCode = """
                       public class C
                       {
                           public void M(int value)
                           {
                               switch (value)
                               {
                                   case 0:
                                   case 1:
                                       break;
                                   default:
                                       break;
                               }
                           }
                       }
                       """
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", "root = true\n[*.cs]\nstylesharp.max_switch_sections = 2\n"));

        await test.RunAsync(CancellationToken.None);
    }
}
