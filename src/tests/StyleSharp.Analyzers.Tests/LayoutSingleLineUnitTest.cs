// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyAccessor = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.AccessorConsistencyAnalyzer,
    StyleSharp.Analyzers.AccessorConsistencyCodeFixProvider>;
using VerifyElement = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.SingleLineElementAnalyzer,
    StyleSharp.Analyzers.SingleLineBlockReflowCodeFixProvider>;
using VerifyStatement = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.SingleLineStatementAnalyzer,
    StyleSharp.Analyzers.SingleLineBlockReflowCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the single-line layout rules (SST1501/SST1502/SST1504).</summary>
public class LayoutSingleLineUnitTest
{
    /// <summary>Verifies a single-line embedded block is reported (SST1501) and expanded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineEmbeddedBlockExpandedAsync()
    {
        const string Source = "internal class C\n{\n    private void M(bool b)\n    {\n        if (b) {|SST1501:{|} b = false; }\n    }\n}";
        const string FixedSource = "internal class C\n{\n    private void M(bool b)\n    {\n        if (b)\n        {\n            b = false;\n        }\n    }\n}";
        await VerifyStatement.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a multi-line embedded block is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiLineEmbeddedBlockIsCleanAsync()
        => await VerifyStatement.VerifyAnalyzerAsync(
            "internal class C\n{\n    private void M(bool b)\n    {\n        if (b)\n        {\n            b = false;\n        }\n    }\n}");

    /// <summary>Verifies a single-line method body is reported (SST1502) and expanded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineMethodBodyExpandedAsync()
    {
        const string Source = "internal class C\n{\n    private void M() {|SST1502:{|} System.Console.WriteLine(); }\n}";
        const string FixedSource = "internal class C\n{\n    private void M()\n    {\n        System.Console.WriteLine();\n    }\n}";
        await VerifyElement.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an empty single-line body is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptySingleLineBodyIsCleanAsync()
        => await VerifyElement.VerifyAnalyzerAsync("internal class C\n{\n    private void M() { }\n}");

    /// <summary>Verifies mixed single-line and multi-line accessors are reported (SST1504) and made consistent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedAccessorsExpandedAsync()
    {
        const string Source = "internal class C\n{\n    private int x;\n\n    public int X\n    {|SST1504:{|}\n"
            + "        get { return x; }\n        set\n        {\n            x = value;\n        }\n    }\n}";
        const string FixedSource = "internal class C\n{\n    private int x;\n\n    public int X\n    {\n        get\n        {\n            return x;\n        }\n"
            + "        set\n        {\n            x = value;\n        }\n    }\n}";
        await VerifyAccessor.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies consistently single-line accessors are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConsistentAccessorsAreCleanAsync()
        => await VerifyAccessor.VerifyAnalyzerAsync(
            "internal class C\n{\n    private int x;\n\n    public int X\n    {\n        get { return x; }\n        set { x = value; }\n    }\n}");
}
