// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConstructor = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MemberDocumentationAnalyzer,
    StyleSharp.Analyzers.ConstructorSummaryCodeFixProvider>;
using VerifyFileName = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.FileNameAnalyzer>;
using VerifyMember = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.MemberDocumentationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Edge-case tests informed by the the analyzer issue tracker (records, structs, period/quotes, Task, inheritdoc, file names).</summary>
public class DocumentationEdgeCaseUnitTest
{
    /// <summary>Verifies a positional record parameter without a &lt;param&gt; is reported (SST1611).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordParameterAsync()
        => await VerifyMember.VerifyAnalyzerAsync(
            "/// <summary>A point.</summary>\n"
            + "public record Point(int {|SST1611:X|});\n"
            + "namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }");

    /// <summary>Verifies a struct constructor summary is rewritten with "struct" (not "class").</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StructConstructorAsync()
    {
        const string Source = "/// <summary>A point.</summary>\n"
            + "public struct Point\n{\n"
            + "    /// {|SST1642:<summary>Makes a point.</summary>|}\n"
            + "    /// <param name=\"x\">The x.</param>\n"
            + "    public Point(int x) { }\n}";
        const string FixedSource = "/// <summary>A point.</summary>\n"
            + "public struct Point\n{\n"
            + "    /// <summary>Initializes a new instance of the <see cref=\"Point\"/> struct.</summary>\n"
            + "    /// <param name=\"x\">The x.</param>\n"
            + "    public Point(int x) { }\n}";

        await VerifyConstructor.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a period tucked inside a closing quote is accepted (no SST1629).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PeriodInsideQuoteAsync()
        => await VerifyMember.VerifyAnalyzerAsync("/// <summary>Sets it to \"true.\"</summary>\npublic class Widget { }");

    /// <summary>Verifies a non-generic Task return does not require a &lt;returns&gt; (no SST1615).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonGenericTaskAsync()
        => await VerifyMember.VerifyAnalyzerAsync(
            "/// <summary>A container.</summary>\n"
            + "public class C\n{\n"
            + "    /// <summary>Does a thing.</summary>\n"
            + "    public System.Threading.Tasks.Task M() => System.Threading.Tasks.Task.CompletedTask;\n}");

    /// <summary>Verifies a summary that inherits via &lt;inheritdoc&gt; is not flagged for casing or emptiness.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritDocSummaryAsync()
        => await VerifyMember.VerifyAnalyzerAsync(
            "/// <summary>A container.</summary>\n"
            + "public class C\n{\n"
            + "    /// <summary><inheritdoc/></summary>\n"
            + "    public int Count { get; set; }\n}");

    /// <summary>Verifies a generic file name like Cache{TKey}.cs matches Cache&lt;TKey&gt;.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericFileNameAsync()
    {
        var test = new VerifyFileName.Test();
        test.TestState.Sources.Add(("Cache{TKey}.cs", "public class Cache<TKey> { }"));
        await test.RunAsync(CancellationToken.None);
    }
}
