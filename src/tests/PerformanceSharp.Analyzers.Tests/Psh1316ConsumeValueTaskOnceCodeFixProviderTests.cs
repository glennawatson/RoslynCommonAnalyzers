// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using RoslynCommon.Analyzers.Tests;
using VerifyConsumeOnce = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1316ConsumeValueTaskOnceAnalyzer,
    PerformanceSharp.Analyzers.Psh1316ConsumeValueTaskOnceCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests safe loop declaration moves and rejected ValueTask fixes.</summary>
public class Psh1316ConsumeValueTaskOnceCodeFixProviderTests
{
    /// <summary>The document name shared by the code-fix fixtures.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>The comment immediately before a synthetic diagnostic.</summary>
    private const string DiagnosticMarker = "/* diagnostic */";

    /// <summary>Verifies each supported loop receives a fresh ValueTask in its body.</summary>
    /// <param name="loop">The loop header.</param>
    /// <param name="ending">The trailing condition for a do loop.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("for (int i = 0; i < 2; i++)", "")]
    [Arguments("foreach (var item in new[] { 1, 2 })", "")]
    [Arguments("while (flag)", "")]
    [Arguments("do", " while (flag);")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task LoopBodyReceivesProducerAsync(string loop, string ending) =>
        VerifyConsumeOnce.VerifyCodeFixAsync(
            $$"""
            using System.Threading.Tasks;
            class C
            {
                async Task M(bool flag)
                {
                    ValueTask<int> vt = new ValueTask<int>(42);
                    {{loop}}
                    {
                        await {|PSH1316:vt|};
                    }{{ending}}
                }
            }
            """,
            $$"""
            using System.Threading.Tasks;
            class C
            {
                async Task M(bool flag)
                {
                    {{loop}}
                    {
                        ValueTask<int> vt = new ValueTask<int>(42);
                        await vt;
                    }{{ending}}
                }
            }
            """);

    /// <summary>Verifies a stale diagnostic cannot move an unsupported declaration or cross a scope boundary.</summary>
    /// <param name="body">The method body containing a marked diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("while (flag) { _ = /* diagnostic */42; }")]
    [Arguments("while (flag) { await /* diagnostic */vt; }")]
    [Arguments("ValueTask vt; while (flag) { await /* diagnostic */vt; }")]
    [Arguments("ValueTask vt = default, other = default; while (flag) { await /* diagnostic */vt; }")]
    [Arguments("using ValueTask vt = default; while (flag) { await /* diagnostic */vt; }")]
    [Arguments("for (ValueTask vt = default; flag;) { await /* diagnostic */vt; }")]
    [Arguments("foreach (ValueTask vt in new ValueTask[1]) { await /* diagnostic */vt; }")]
    [Arguments("if (value is ValueTask vt) { while (flag) { await /* diagnostic */vt; } }")]
    [Arguments("ValueTask vt = default; while (flag) await /* diagnostic */vt;")]
    [Arguments("ValueTask vt = default; await /* diagnostic */vt;")]
    [Arguments("ValueTask vt = default; while (flag) { System.Func<Task> action = async () => { await /* diagnostic */vt; }; }")]
    [Arguments("ValueTask vt = default; while (flag) { async Task Local() { await /* diagnostic */vt; } }")]
    [Arguments("ValueTask vt = default; foreach (var (first, second) in new[] { (1, 2) }) { await /* diagnostic */vt; }")]
    [Arguments("ValueTask vt = default; _ = vt; while (flag) { await /* diagnostic */vt; }")]
    [Arguments("ValueTask vt = default; while (flag) { await /* diagnostic */vt; } _ = vt;")]
    [Arguments("ValueTask vt = default; while (vt.IsCompleted) { await /* diagnostic */vt; }")]
    [Arguments("ValueTask vt = default;\n#region Consume\nwhile (flag) { await /* diagnostic */vt; }\n#endregion\n")]
    public async Task UnsupportedMoveRegistersNoActionAsync(string body)
    {
        var source = $$"""
            using System.Threading.Tasks;
            class C
            {
                async Task M(bool flag, object value)
                {
                    {{body}}
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var span = root.FindToken(source.IndexOf(DiagnosticMarker, StringComparison.Ordinal) + DiagnosticMarker.Length).Span;
        var diagnostic = Diagnostic.Create(ConcurrencyRules.ConsumeValueTaskOnce, root.SyntaxTree.GetLocation(span), "vt");
        using var container = new ContainerConfiguration().WithPart<Psh1316ConsumeValueTaskOnceCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That((await document.GetTextAsync()).ToString()).IsEqualTo(source);
    }

    /// <summary>Verifies parameters, fields, and top-level locals cannot be moved into a loop body.</summary>
    /// <param name="source">The source containing a marked diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System.Threading.Tasks; class C { async Task M(ValueTask vt) { while (true) { await /* diagnostic */vt; } } }")]
    [Arguments("using System.Threading.Tasks; class C { ValueTask vt; async Task M() { while (true) { await /* diagnostic */vt; } } }")]
    [Arguments("using System.Threading.Tasks; ValueTask vt = default; while (true) { await /* diagnostic */vt; }")]
    [Arguments("using System.Threading.Tasks; ValueTask vt = default; await /* diagnostic */vt;")]
    public async Task MissingLocalBlockRegistersNoActionAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication))
            .AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var span = root.FindToken(source.IndexOf(DiagnosticMarker, StringComparison.Ordinal) + DiagnosticMarker.Length).Span;
        var diagnostic = Diagnostic.Create(ConcurrencyRules.ConsumeValueTaskOnce, root.SyntaxTree.GetLocation(span), "vt");
        using var container = new ContainerConfiguration().WithPart<Psh1316ConsumeValueTaskOnceCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Verifies other symbols with the same name and additional uses inside the loop permit a move.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnrelatedSameNameSymbolDoesNotPreventMoveAsync()
    {
        const string Source = """
            using System.Threading.Tasks;
            class C
            {
                ValueTask vt;
                async Task M(bool flag)
                {
                    ValueTask vt = default;
                    while (flag)
                    {
                        await /* diagnostic */vt;
                        _ = vt.IsCompleted;
                    }
                    _ = this.vt;
                }
            }
            """;
        const string Expected = """
            using System.Threading.Tasks;
            class C
            {
                ValueTask vt;
                async Task M(bool flag)
                {
                    while (flag)
                    {
                        ValueTask vt = default;
                        await /* diagnostic */vt;
                        _ = vt.IsCompleted;
                    }
                    _ = this.vt;
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(DocumentName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var span = root.FindToken(Source.IndexOf(DiagnosticMarker, StringComparison.Ordinal) + DiagnosticMarker.Length).Span;
        var diagnostic = Diagnostic.Create(ConcurrencyRules.ConsumeValueTaskOnce, root.SyntaxTree.GetLocation(span), "vt");
        using var container = new ContainerConfiguration().WithPart<Psh1316ConsumeValueTaskOnceCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await Assert.That(provider.FixableDiagnosticIds).Contains("PSH1316");
        await Assert.That(provider.GetFixAllProvider()).IsSameReferenceAs(WellKnownFixAllProviders.BatchFixer);
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(actions[0].Title).IsEqualTo("Move the producing call into the loop");
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(Expected);
    }

    /// <summary>Verifies a stale condition diagnostic currently moves a producer into an empty body.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConditionDiagnosticMovesProducerIntoEmptyBodyAsync()
    {
        const string Source = "using System.Threading.Tasks; class C { void M() { ValueTask vt = default; while (/* diagnostic */vt.IsCompleted) { } } }";
        const string Expected = "using System.Threading.Tasks; class C { void M() { while (/* diagnostic */vt.IsCompleted) { ValueTask vt = default; } } }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(DocumentName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var span = root.FindToken(Source.IndexOf(DiagnosticMarker, StringComparison.Ordinal) + DiagnosticMarker.Length).Span;
        var diagnostic = Diagnostic.Create(ConcurrencyRules.ConsumeValueTaskOnce, root.SyntaxTree.GetLocation(span), "vt");
        using var container = new ContainerConfiguration().WithPart<Psh1316ConsumeValueTaskOnceCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(Expected);
    }
}
