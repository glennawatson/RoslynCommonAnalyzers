// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;

using VerifyAnalyzer = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<PerformanceSharp.Analyzers.Psh1126UseAnyAsyncOverCountAsyncAnalyzer>;
using VerifyAnyAsync = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1126UseAnyAsyncOverCountAsyncAnalyzer,
    PerformanceSharp.Analyzers.Psh1126UseAnyAsyncOverCountAsyncCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1126 (ask an async sequence for elements without counting them) and its code fix.</summary>
public class UseAnyAsyncOverCountAsyncAnalyzerUnitTest
{
    /// <summary>A stand-in async query provider, mirroring the extension-method shape real providers ship.</summary>
    private const string Provider = """
                                    using System.Linq;
                                    using System.Threading;
                                    using System.Threading.Tasks;

                                    public static class AsyncQuery
                                    {
                                        public static Task<int> CountAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) => Task.FromResult(0);

                                        public static Task<bool> AnyAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) => Task.FromResult(false);
                                    }

                                    """;

    /// <summary>A provider that can count but has no AnyAsync sibling to move to.</summary>
    private const string ProviderWithoutAny = """
                                              using System.Linq;
                                              using System.Threading;
                                              using System.Threading.Tasks;

                                              public static class AsyncQuery
                                              {
                                                  public static Task<int> CountAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) => Task.FromResult(0);
                                              }

                                              """;

    /// <summary>Checks the replacement belongs to the same provider and accepts the same receiver and arguments.</summary>
    /// <param name="countType">The counting method's return type.</param>
    /// <param name="sibling">The potential replacement declaration.</param>
    /// <param name="report">Whether an applicable replacement exists.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Task<int>", "public static Task<bool> AnyAsync(this C source, int value) => null;", true)]
    [Arguments("ValueTask<int>", "public static ValueTask<bool> AnyAsync(this C source, int value) => default;", true)]
    [Arguments("Task<long>", "public static Task<bool> AnyAsync(this C source, int value) => null;", false)]
    [Arguments("Task", "public static Task<bool> AnyAsync(this C source, int value) => null;", false)]
    [Arguments("Custom<int>", "public static Task<bool> AnyAsync(this C source, int value) => null;", false)]
    [Arguments("Task<int>", "public static int AnyAsync;", false)]
    [Arguments("Task<int>", "public static Task<bool> AnyAsync(C source, int value) => null;", false)]
    [Arguments("Task<int>", "public Task<bool> AnyAsync(C source, int value) => null;", false)]
    [Arguments("Task<int>", "public static Task<bool> AnyAsync(this string source, int value) => null;", false)]
    [Arguments("Task<int>", "public static Task<int> AnyAsync(this C source, int value) => null;", false)]
    [Arguments("Task<int>", "public static Task AnyAsync(this C source, int value) => null;", false)]
    [Arguments("Task<int>", "public static Custom<bool> AnyAsync(this C source, int value) => null;", false)]
    [Arguments("Task<int>", "public static Task<bool> AnyAsync(this C source) => null;", false)]
    [Arguments("Task<int>", "public static Task<bool> AnyAsync(this C source, string value) => null;", false)]
    [Arguments("Task<int>", "public static Task<bool> AnyAsync(this string source, int value) => null; public static Task<bool> AnyAsync(this C source, int value) => null;", true)]
    public async Task SiblingMustMatchAwaitableReceiverAndParametersAsync(string countType, string sibling, bool report)
    {
        var source = $$"""
            using System.Threading.Tasks;
            public class Custom<T> { }
            public static class Provider
            {
                public static {{countType}} CountAsync(this C source, int value) => default;
                {{sibling}}
            }
            public class C
            {
                public async Task<bool> M() => (await this.CountAsync(1)) > 0;
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(nameof(SiblingMustMatchAwaitableReceiverAndParametersAsync), [tree], RuntimeMetadataReferences.Platform, new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Psh1126UseAnyAsyncOverCountAsyncAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(report ? 1 : 0);
        if (!report)
        {
            return;
        }

        await Assert.That(diagnostics[0].Id).IsEqualTo("PSH1126");
        var root = await tree.GetRootAsync();
        await Assert.That(root.FindNode(diagnostics[0].Location.SourceSpan).ToString()).IsEqualTo("(await this.CountAsync(1)) > 0");
    }

    /// <summary>Checks missing framework types, unresolved calls, and instance methods never produce a recommendation.</summary>
    /// <param name="source">The source containing the near-miss comparison.</param>
    /// <param name="hasFramework">Whether framework references are available.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { async void M(C c) { if (await c.CountAsync() > 0) { } } }", false)]
    [Arguments("class C { async System.Threading.Tasks.Task<bool> M(C c) => await c.CountAsync() > 0; }", true)]
    [Arguments(
        """
        using System.Threading.Tasks;
        static class Provider
        {
            public static T CountAsync<T>(this C source, T value) => value;
            public static Task<bool> AnyAsync<T>(this C source, T value) => null;
        }
        class C { async Task<bool> M<T>(T value) => await this.CountAsync(value) > 0; }
        """,
        true)]
    [Arguments(
        """
        using System.Threading.Tasks;
        class C
        {
            public Task<int> CountAsync() => null;
            public Task<bool> AnyAsync() => null;
            async Task<bool> M(C c) => await c.CountAsync() > 0;
        }
        """,
        true)]
    public async Task MissingFrameworkOrExtensionSymbolIsIgnoredAsync(string source, bool hasFramework)
    {
        var references = hasFramework ? RuntimeMetadataReferences.Platform : [];
        var compilation = CSharpCompilation.Create(nameof(MissingFrameworkOrExtensionSymbolIsIgnoredAsync), [CSharpSyntaxTree.ParseText(source)], references, new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Psh1126UseAnyAsyncOverCountAsyncAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Checks the syntax gate requires an awaited member call and an emptiness comparison.</summary>
    /// <param name="expression">The comparison to classify.</param>
    /// <param name="hasElements">The expected result, or null when the shape is unsupported.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("((await q.CountAsync())) != 0", true)]
    [Arguments("await q.CountAsync() >= 1", true)]
    [Arguments("await q.CountAsync() < 1", false)]
    [Arguments("1 > await q.CountAsync()", false)]
    [Arguments("0 >= await q.CountAsync()", false)]
    [Arguments("q.CountAsync() > 0", null)]
    [Arguments("await CountAsync() > 0", null)]
    [Arguments("await q.OtherAsync() > 0", null)]
    [Arguments("await q.CountAsync > 0", null)]
    [Arguments("await (q.CountAsync()) > 0", null)]
    [Arguments("await q?.CountAsync() > 0", null)]
    [Arguments("await q->CountAsync() > 0", null)]
    [Arguments("await q.CountAsync() == 2", null)]
    [Arguments("await q.CountAsync() > await r.CountAsync()", null)]
    public async Task ComparisonShapeRequiresAwaitedMemberInvocationAsync(string expression, bool? hasElements)
    {
        var binary = (BinaryExpressionSyntax)SyntaxFactory.ParseExpression(expression);
        var shape = Psh1126UseAnyAsyncOverCountAsyncAnalyzer.TryGetComparisonShape(binary);
        await Assert.That(shape?.HasElements).IsEqualTo(hasElements);
    }

    /// <summary>Checks a function pointer signature has no receiver for an extension replacement.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FunctionPointerWithoutReceiverHasNoSiblingAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("unsafe class C { public delegate*<System.Threading.Tasks.Task<int>> CountAsync; }");
        var compilation = CSharpCompilation.Create(
            nameof(FunctionPointerWithoutReceiverHasNoSiblingAsync),
            [tree],
            RuntimeMetadataReferences.Platform,
            new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("CountAsync").Single();
        var method = ((IFunctionPointerTypeSymbol)field.Type).Signature;
        var awaitables = new Psh1126UseAnyAsyncOverCountAsyncAnalyzer.AwaitableTypes(compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1")!, null);
        await Assert.That(method.ReceiverType).IsNull();
        await Assert.That(Psh1126UseAnyAsyncOverCountAsyncAnalyzer.TryResolveAnySibling(method, awaitables)).IsNull();
    }

    /// <summary>Verifies an awaited CountAsync() &gt; 0 is reported and rewritten to AnyAsync().</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountAsyncGreaterThanZeroReplacedWithAnyAsync()
    {
        const string Body = """
                            public class C
                            {
                                public async Task<bool> M(IQueryable<int> query) => {|PSH1126:await query.CountAsync() > 0|};
                            }
                            """;
        const string FixedBody = """
                                 public class C
                                 {
                                     public async Task<bool> M(IQueryable<int> query) => await query.AnyAsync();
                                 }
                                 """;
        await VerifyNet90Async(Provider + Body, Provider + FixedBody);
    }

    /// <summary>Checks repeated comparisons in one compilation reuse the resolved awaitable types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public Task RepeatedComparisonsReportBothEmptinessChecksAsync()
    {
        const string Body = """
            class C
            {
                async Task<bool> M(IQueryable<int> query)
                    => {|PSH1126:await query.CountAsync() > 0|} && {|PSH1126:await query.CountAsync() == 0|};
            }
            """;
        var test = new VerifyAnalyzer.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = Provider + Body };
        return test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an awaited CountAsync() == 0 is reported and rewritten to a negated AnyAsync().</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountAsyncEqualsZeroReplacedWithNegatedAnyAsync()
    {
        const string Body = """
                            public class C
                            {
                                public async Task<bool> M(IQueryable<int> query) => {|PSH1126:await query.CountAsync() == 0|};
                            }
                            """;
        const string FixedBody = """
                                 public class C
                                 {
                                     public async Task<bool> M(IQueryable<int> query) => !await query.AnyAsync();
                                 }
                                 """;
        await VerifyNet90Async(Provider + Body, Provider + FixedBody);
    }

    /// <summary>Verifies the reversed operand order is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReversedOperandOrderReplacedWithAnyAsync()
    {
        const string Body = """
                            public class C
                            {
                                public async Task<bool> M(IQueryable<int> query) => {|PSH1126:0 < await query.CountAsync()|};
                            }
                            """;
        const string FixedBody = """
                                 public class C
                                 {
                                     public async Task<bool> M(IQueryable<int> query) => await query.AnyAsync();
                                 }
                                 """;
        await VerifyNet90Async(Provider + Body, Provider + FixedBody);
    }

    /// <summary>Verifies a cancellation-token argument is carried over to the AnyAsync call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CancellationTokenArgumentIsCarriedOverAsync()
    {
        const string Body = """
                            public class C
                            {
                                public async Task<bool> M(IQueryable<int> query, CancellationToken token)
                                    => {|PSH1126:await query.CountAsync(token) > 0|};
                            }
                            """;
        const string FixedBody = """
                                 public class C
                                 {
                                     public async Task<bool> M(IQueryable<int> query, CancellationToken token)
                                         => await query.AnyAsync(token);
                                 }
                                 """;
        await VerifyNet90Async(Provider + Body, Provider + FixedBody);
    }

    /// <summary>Verifies a provider with no AnyAsync sibling is never reported, so no unfixable diagnostic appears.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProviderWithoutAnyAsyncIsNotReportedAsync()
    {
        const string Body = """
                            public class C
                            {
                                public async Task<bool> M(IQueryable<int> query) => await query.CountAsync() > 0;
                            }
                            """;
        await VerifyNet90Async(ProviderWithoutAny + Body, ProviderWithoutAny + Body);
    }

    /// <summary>Verifies a comparison that needs the real count is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparisonAgainstNonZeroIsNotReportedAsync()
    {
        const string Body = """
                            public class C
                            {
                                public async Task<bool> M(IQueryable<int> query) => await query.CountAsync() > 5;
                            }
                            """;
        await VerifyNet90Async(Provider + Body, Provider + Body);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyAnyAsync.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source, FixedCode = fixedSource };

        return test.RunAsync(CancellationToken.None);
    }
}
