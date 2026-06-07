// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyChain = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ChainedBlockSpacingAnalyzer,
    StyleSharp.Analyzers.ChainedBlockSpacingCodeFixProvider>;
using VerifyFileEnd = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.FileEndingAnalyzer,
    StyleSharp.Analyzers.FileEndingCodeFixProvider>;
using VerifyFileStart = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.FileStartBlankLinesAnalyzer,
    StyleSharp.Analyzers.FileStartBlankLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the file-boundary and chained-block layout rules (SST1510/SST1511/SST1517/SST1518).</summary>
public class LayoutFileAndChainUnitTest
{
    /// <summary>Verifies a blank line at the start of the file is reported (SST1517) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLinesAtFileStartRemovedAsync()
        => await VerifyFileStart.VerifyCodeFixAsync(
            "\ninternal class C\n{\n}\n",
            VerifyFileStart.Diagnostic("SST1517").WithSpan(1, 1, 2, 1),
            "internal class C\n{\n}\n");

    /// <summary>Verifies a missing final newline is reported (SST1518) and added.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingFinalNewlineAddedAsync()
        => await VerifyFileEnd.VerifyCodeFixAsync(
            "internal class C\n{\n}",
            VerifyFileEnd.Diagnostic("SST1518").WithSpan(3, 2, 3, 2),
            "internal class C\n{\n}\n");

    /// <summary>Verifies a blank line before 'else' is reported (SST1510) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankBeforeElseRemovedAsync()
    {
        const string Source = "internal class C\n{\n    private void M(bool x)\n    {\n        if (x)\n        {\n        }\n\n        {|SST1510:else|}\n        {\n        }\n    }\n}";
        const string FixedSource = "internal class C\n{\n    private void M(bool x)\n    {\n        if (x)\n        {\n        }\n        else\n        {\n        }\n    }\n}";
        await VerifyChain.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blank line before a do/while footer is reported (SST1511) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankBeforeWhileFooterRemovedAsync()
    {
        const string Source = "internal class C\n{\n    private void M(bool x)\n    {\n        do\n        {\n        }\n\n        {|SST1511:while|} (x);\n    }\n}";
        const string FixedSource = "internal class C\n{\n    private void M(bool x)\n    {\n        do\n        {\n        }\n        while (x);\n    }\n}";
        await VerifyChain.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an 'else' that directly follows the if block is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AdjacentElseIsCleanAsync()
        => await VerifyChain.VerifyAnalyzerAsync(
            "internal class C\n{\n    private void M(bool x)\n    {\n        if (x)\n        {\n        }\n        else\n        {\n        }\n    }\n}");
}
