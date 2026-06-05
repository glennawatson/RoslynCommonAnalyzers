// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.MemberDocumentationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the documentation quality rules (SST1608/1612/1613/1614/1616/1620/1621/1622).</summary>
public class DocumentationQualityUnitTest
{
    /// <summary>Verifies a placeholder summary is reported (SST1608).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultSummaryAsync()
        => await Verify.VerifyAnalyzerAsync("/// {|SST1608:<summary>Summary description here.</summary>|}\npublic class Widget { }");

    /// <summary>Verifies an extra parameter documentation element is reported (SST1612).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterMatchAsync()
    {
        const string source = "/// <summary>A container.</summary>\n"
            + "public class C\n{\n"
            + "    /// <summary>Does a thing.</summary>\n"
            + "    /// <param name=\"value\">The value.</param>\n"
            + "    /// {|SST1612:<param name=\"extra\">Extra.</param>|}\n"
            + "    public void M(int value) { }\n}";

        await Verify.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies a nameless parameter documentation element is reported (SST1613).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterNameAsync()
        => await Verify.VerifyAnalyzerAsync("/// <summary>A widget.</summary>\n/// {|SST1613:<param>No name.</param>|}\npublic class Widget { }");

    /// <summary>Verifies an empty parameter documentation element is reported (SST1614).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterTextAsync()
    {
        const string source = "/// <summary>A container.</summary>\n"
            + "public class C\n{\n"
            + "    /// <summary>Does a thing.</summary>\n"
            + "    /// <param name=\"value\"></param>\n"
            + "    public void M(int {|SST1614:value|}) { }\n}";

        await Verify.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies an empty return documentation element is reported (SST1616).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnTextAsync()
    {
        const string source = "/// <summary>A container.</summary>\n"
            + "public class C\n{\n"
            + "    /// <summary>Gets a value.</summary>\n"
            + "    /// {|SST1616:<returns></returns>|}\n"
            + "    public int M() => 0;\n}";

        await Verify.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies an extra type parameter documentation element is reported (SST1620).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterMatchAsync()
    {
        const string source = "/// <summary>A container.</summary>\n"
            + "public class C\n{\n"
            + "    /// <summary>Does a thing.</summary>\n"
            + "    /// <typeparam name=\"T\">The type.</typeparam>\n"
            + "    /// {|SST1620:<typeparam name=\"X\">Extra.</typeparam>|}\n"
            + "    public void M<T>() { }\n}";

        await Verify.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies a nameless type parameter documentation element is reported (SST1621).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterNameAsync()
        => await Verify.VerifyAnalyzerAsync("/// <summary>A widget.</summary>\n/// {|SST1621:<typeparam>No name.</typeparam>|}\npublic class Widget { }");

    /// <summary>Verifies an empty type parameter documentation element is reported (SST1622).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterTextAsync()
    {
        const string source = "/// <summary>A container.</summary>\n"
            + "public class C\n{\n"
            + "    /// <summary>Does a thing.</summary>\n"
            + "    /// <typeparam name=\"T\"></typeparam>\n"
            + "    public void M<{|SST1622:T|}>() { }\n}";

        await Verify.VerifyAnalyzerAsync(source);
    }
}
