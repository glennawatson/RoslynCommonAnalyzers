// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for the consistent-line-endings rule (SST1532). The Microsoft test verifier normalises
/// line endings in its source text, so these exercise the analyzer and code fix over a real compilation
/// whose source keeps its carriage returns.
/// </summary>
public class FileLineEndingUnitTest
{
    /// <summary>Verifies carriage-return/line-feed endings are reported and normalised to line feed by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CarriageReturnLineFeedNormalisedToLineFeedByDefaultAsync()
        => await AssertAsync(
            "internal class C\r\n{\r\n}\r\n",
            "dotnet_diagnostic.SST1532.severity = warning",
            expectedDiagnostics: 1,
            "internal class C\n{\n}\n");

    /// <summary>Verifies line-feed endings are reported and normalised to CRLF when 'crlf' is configured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LineFeedNormalisedToCarriageReturnLineFeedWhenConfiguredAsync()
        => await AssertAsync(
            "internal class C\n{\n}\n",
            "dotnet_diagnostic.SST1532.severity = warning\nstylesharp.line_ending = crlf",
            expectedDiagnostics: 1,
            "internal class C\r\n{\r\n}\r\n");

    /// <summary>Verifies a file whose endings already all match the configured style is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConsistentLineFeedFileIsCleanAsync()
        => await AssertAsync(
            "internal class C\n{\n}\n",
            "dotnet_diagnostic.SST1532.severity = warning",
            expectedDiagnostics: 0,
            "internal class C\n{\n}\n");

    /// <summary>Runs the analyzer and, when a diagnostic is expected, applies the code fix and checks the result.</summary>
    /// <param name="source">The source whose exact line endings are preserved.</param>
    /// <param name="configBody">The editorconfig body enabling and configuring the rule.</param>
    /// <param name="expectedDiagnostics">The expected diagnostic count.</param>
    /// <param name="expectedFixed">The expected source after the fix.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task AssertAsync(string source, string configBody, int expectedDiagnostics, string expectedFixed)
    {
        using var workspace = new AdhocWorkspace();
        var config = "root = true\n[*.cs]\n" + configBody + "\n";
        var project = workspace.CurrentSolution
            .AddProject("Test", "Test", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
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
