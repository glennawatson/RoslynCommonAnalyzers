// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for the consistent-line-endings rule (SST1532). The Microsoft test verifier normalises
/// line endings in its source text, so these exercise the analyzer and code fix over a real compilation
/// whose source keeps its carriage returns.
/// </summary>
[SuppressMessage(
    "Correctness",
    "SST2473:A shared export part should be obtained from the container, not constructed with 'new'",
    Justification = "The code-fix provider is the subject of the test, so it has to be constructed directly to be exercised.")]
public class FileLineEndingUnitTest
{
    /// <summary>The sample class source written with line-feed endings throughout.</summary>
    private const string SourceWithLineFeedEndings = "internal class C\n{\n}\n";

    /// <summary>Verifies registered actions normalize mixed endings and leave matching text intact.</summary>
    /// <param name="source">The exact original line endings.</param>
    /// <param name="target">The requested newline sequence.</param>
    /// <param name="expected">The exact resulting source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C\r\n{\n}\r", "\n", "class C\n{\n}\n")]
    [Arguments("class C\n{\r\n}", "\r\n", "class C\r\n{\r\n}")]
    [Arguments("class C\n{\n}\n", "\n", "class C\n{\n}\n")]
    public async Task RegisteredActionNormalizesEndingsAsync(string source, string target, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var properties = ImmutableDictionary<string, string?>.Empty.Add(Sst1532ConsistentLineEndingsAnalyzer.LineEndingProperty, target);
        var diagnostic = Diagnostic.Create(LayoutRules.ConsistentLineEndings, root.GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst1532ConsistentLineEndingsCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await Assert.That(provider.FixableDiagnosticIds).Contains("SST1532");
        await Assert.That(provider.GetFixAllProvider()).IsNotNull();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
    }

    /// <summary>Verifies missing or null newline properties register no action or text changes.</summary>
    /// <param name="includeProperty">Whether the diagnostic includes a null-valued property.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MissingTargetIsIgnoredAsync(bool includeProperty)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", SourceWithLineFeedEndings);
        var root = (await document.GetSyntaxRootAsync())!;
        var properties = includeProperty
            ? ImmutableDictionary<string, string?>.Empty.Add(Sst1532ConsistentLineEndingsAnalyzer.LineEndingProperty, null)
            : ImmutableDictionary<string, string?>.Empty;
        var diagnostic = Diagnostic.Create(LayoutRules.ConsistentLineEndings, root.GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst1532ConsistentLineEndingsCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var changes = new List<TextChange>();
        ((ITextChangeBatchableCodeFix)provider).RegisterTextChanges(await document.GetTextAsync(), root, diagnostic, changes);
        await Assert.That(changes).IsEmpty();
    }

    /// <summary>Verifies carriage-return/line-feed endings are reported and normalised to line feed by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CarriageReturnLineFeedNormalisedToLineFeedByDefaultAsync() =>
        AssertAsync(
            "internal class C\r\n{\r\n}\r\n",
            "dotnet_diagnostic.SST1532.severity = warning",
            expectedDiagnostics: 1,
            SourceWithLineFeedEndings);

    /// <summary>Verifies line-feed endings are reported and normalised to CRLF when 'crlf' is configured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LineFeedNormalisedToCarriageReturnLineFeedWhenConfiguredAsync() =>
        AssertAsync(
            SourceWithLineFeedEndings,
            "dotnet_diagnostic.SST1532.severity = warning\nstylesharp.line_ending = crlf",
            expectedDiagnostics: 1,
            "internal class C\r\n{\r\n}\r\n");

    /// <summary>Verifies a file whose endings already all match the configured style is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConsistentLineFeedFileIsCleanAsync() =>
        AssertAsync(
            SourceWithLineFeedEndings,
            "dotnet_diagnostic.SST1532.severity = warning",
            expectedDiagnostics: 0,
            SourceWithLineFeedEndings);

    /// <summary>Runs the analyzer and, when a diagnostic is expected, applies the code fix and checks the result.</summary>
    /// <param name="source">The source whose exact line endings are preserved.</param>
    /// <param name="configBody">The editorconfig body enabling and configuring the rule.</param>
    /// <param name="expectedDiagnostics">The expected diagnostic count.</param>
    /// <param name="expectedFixed">The expected source after the fix.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task AssertAsync(string source, string configBody, int expectedDiagnostics, string expectedFixed)
    {
        using var workspace = new AdhocWorkspace();
        var config = $"root = true\n[*.cs]\n{configBody}\n";
        var project = workspace.CurrentSolution
            .AddProject(nameof(Test), nameof(Test), LanguageNames.CSharp)
            .AddMetadataReference(RuntimeMetadataReferences.CoreLibrary)
            .AddAnalyzerConfigDocument("/.editorconfig", SourceText.From(config), filePath: "/.editorconfig").Project;
        var document = project.AddDocument("Test0.cs", SourceText.From(source), filePath: "/Test0.cs");
        project = document.Project;

        var compilation = (await project.GetCompilationAsync(CancellationToken.None))!;
        ImmutableArray<DiagnosticAnalyzer> analyzers = [new Sst1532ConsistentLineEndingsAnalyzer()];
        var withAnalyzers = compilation.WithAnalyzers(analyzers, project.AnalyzerOptions);
        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);

        await Assert.That(diagnostics.Length).IsEqualTo(expectedDiagnostics);
        if (expectedDiagnostics == 0)
        {
            return;
        }

        var text = await document.GetTextAsync(CancellationToken.None);
        var root = (await document.GetSyntaxRootAsync(CancellationToken.None))!;
        List<TextChange> changes = [];
        ((ITextChangeBatchableCodeFix)new Sst1532ConsistentLineEndingsCodeFixProvider()).RegisterTextChanges(text, root, diagnostics[0], changes);
        await Assert.That(text.WithChanges(changes).ToString()).IsEqualTo(expectedFixed);
    }
}
