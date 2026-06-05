// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.SingleLineSummaryAnalyzer,
    StyleSharp.Analyzers.SingleLineSummaryCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1653 (short summaries should be on a single line).</summary>
public class SingleLineSummaryAnalyzerUnitTest
{
    /// <summary>Verifies a single-line summary produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidSingleLineAsync()
        => await Verify.VerifyAnalyzerAsync("/// <summary>Short text.</summary>\npublic class C { }");

    /// <summary>Verifies a long multi-line summary (over the limit) produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidLongMultiLineAsync()
        => await Verify.VerifyAnalyzerAsync(
            "/// <summary>\n"
            + "/// This is a deliberately long summary that comfortably exceeds the configured one hundred character single-line limit.\n"
            + "/// </summary>\n"
            + "public class C { }");

    /// <summary>Verifies an empty multi-line summary is ignored.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptySummaryIgnoredAsync()
        => await Verify.VerifyAnalyzerAsync("/// <summary>\n/// </summary>\npublic class C { }");

    /// <summary>Verifies a short multi-line summary is reported and collapsed onto one line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShortMultiLineCollapsedAsync()
        => await Verify.VerifyCodeFixAsync(
            "/// {|SST1653:<summary>\n/// Short text.\n/// </summary>|}\npublic class C { }",
            "/// <summary>Short text.</summary>\npublic class C { }");

    /// <summary>Verifies lowering the limit via editorconfig stops a short summary from being reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfiguredLimitAsync()
    {
        var test = new Verify.Test
        {
            TestCode = "/// <summary>\n/// Short text.\n/// </summary>\npublic class C { }",
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", "root = true\n[*.cs]\nstylesharp.summary_single_line_max_length = 5\n"));

        await test.RunAsync(CancellationToken.None);
    }
}
