// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyPlacement = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1145ConditionalOperatorPlacementAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1145 (place conditional operators consistently).</summary>
public class ConditionalOperatorPlacementAnalyzerUnitTest
{
    /// <summary>Verifies trailing operators are reported under the default (leading) placement.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingOperatorsReportedAsync()
        => await VerifyPlacement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(bool c) => c {|SST1145:?|}
                    1 {|SST1145::|}
                    2;
            }
            """);

    /// <summary>Verifies leading operators and a single-line conditional are not reported by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingAndSingleLineAreCleanAsync()
        => await VerifyPlacement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(bool c) => c
                    ? 1
                    : 2;

                public int N(bool c) => c ? 1 : 2;
            }
            """);

    /// <summary>Verifies leading operators are reported when trailing placement is configured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingConfiguredAsync()
    {
        var test = new VerifyPlacement.Test
        {
            TestCode = """
                       public class C
                       {
                           public int M(bool c) => c
                               {|SST1145:?|} 1
                               {|SST1145::|} 2;
                       }
                       """
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig",
             """
             root = true
             [*.cs]
             stylesharp.conditional_operator_placement = trailing

             """));

        await test.RunAsync(CancellationToken.None);
    }
}
