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
            """
            /// <summary>A point.</summary>
            public record Point(int {|SST1611:X|});
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """);

    /// <summary>Verifies a struct constructor summary is rewritten with "struct" (not "class").</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StructConstructorAsync()
    {
        const string Source = """
            /// <summary>A point.</summary>
            public struct Point
            {
                /// {|SST1642:<summary>Makes a point.</summary>|}
                /// <param name="x">The x.</param>
                public Point(int x) { }
            }
            """;
        const string FixedSource = """
            /// <summary>A point.</summary>
            public struct Point
            {
                /// <summary>Initializes a new instance of the <see cref="Point"/> struct.</summary>
                /// <param name="x">The x.</param>
                public Point(int x) { }
            }
            """;

        await VerifyConstructor.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a period tucked inside a closing quote is accepted (no SST1629).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PeriodInsideQuoteAsync()
        => await VerifyMember.VerifyAnalyzerAsync(
            """
            /// <summary>Sets it to "true."</summary>
            public class Widget { }
            """);

    /// <summary>Verifies a non-generic Task return still requires a &lt;returns&gt; (SST1615).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonGenericTaskAsync()
        => await VerifyMember.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                public System.Threading.Tasks.Task {|SST1615:M|}() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    /// <summary>Verifies a non-generic ValueTask return still requires a &lt;returns&gt; (SST1615).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonGenericValueTaskAsync()
        => await VerifyMember.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                public System.Threading.Tasks.ValueTask {|SST1615:M|}() => default;
            }
            """);

    /// <summary>Verifies a Task-returning member with conditional compilation is not treated as void-like (no SST1617).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalTaskExpressionBodyAsync()
        => await VerifyMember.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.CodeAnalysis;
            namespace N;

            /// <summary>API approval tests.</summary>
            [ExcludeFromCodeCoverage]
            public class ApiApprovalTests
            {
                /// <summary>Generates the public API for the assembly.</summary>
                /// <returns>A task to monitor the process.</returns>
                public System.Threading.Tasks.Task Wpf() =>
            #if WINDOWS
                    System.Threading.Tasks.Task.CompletedTask;
            #else
                    System.Threading.Tasks.Task.CompletedTask;
            #endif
            }
            """);

    /// <summary>Verifies a summary that inherits via &lt;inheritdoc&gt; is not flagged for casing or emptiness.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritDocSummaryAsync()
        => await VerifyMember.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary><inheritdoc/></summary>
                public int Count { get; set; }
            }
            """);

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
