// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.FileHeaderAnalyzer,
    StyleSharp.Analyzers.FileHeaderCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

    /// <summary>Unit tests for SST1633 (file header from file_header_template).</summary>
public class FileHeaderAnalyzerUnitTest
{
    /// <summary>The editorconfig configuring a single-line header template.</summary>
    private const string EditorConfig = """
        root = true
        [*.cs]
        file_header_template = Copyright text.

        """;

    /// <summary>Verifies a file with no header template configured is ignored.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsetIgnoredAsync()
        => await Verify.VerifyAnalyzerAsync("namespace N { }");

    /// <summary>Verifies a file with the correct header produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidHeaderAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                // Copyright text.
                namespace N { }
                """
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EditorConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a file missing its header is reported and the header is inserted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingHeaderAsync()
    {
        var test = new Verify.Test
        {
            TestCode = "namespace N { }",

            // Normalized to line feeds. The source is a single line with no break for the fix to
            // detect, so it inserts its default line feed. A carriage-return checkout would otherwise
            // leave the expected snippet with different breaks and never match.
            FixedCode = """
                // Copyright text.
                namespace N { }
                """.ReplaceLineEndings("\n"),

            // A file-start (position 0) diagnostic cannot be suppressed by a #pragma
            // that necessarily comes after it, so skip the harness's suppression check.
            TestBehaviors = TestBehaviors.SkipSuppressionCheck
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EditorConfig));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", EditorConfig));
        test.ExpectedDiagnostics.Add(Verify.Diagnostic("SST1633").WithSpan(1, 1, 1, 1));
        await test.RunAsync(CancellationToken.None);
    }
}
