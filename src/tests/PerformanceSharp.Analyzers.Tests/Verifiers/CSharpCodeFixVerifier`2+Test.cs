// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace PerformanceSharp.Analyzers.Tests;

/// <content>
/// Contains the nested <see cref="Test"/> type used to run C# code fix verification.
/// </content>
public static partial class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    /// <summary>A configured C# code fix test that enables nullable reference type warnings during validation.</summary>
    public class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
    {
        /// <summary>Where the newline config goes, beside the sources and clear of each test's own "/.editorconfig".</summary>
        private const string NestedEditorConfigPath = "/0/.editorconfig";

        /// <summary>The config pinning LF, matching the line endings the expected sources are written with.</summary>
        private const string LineFeedConfig = "[*]\nend_of_line = lf\n";

        /// <summary>The config pinning CRLF, as a repo that stores CRLF would.</summary>
        private const string CarriageReturnLineFeedConfig = "[*]\nend_of_line = crlf\n";

        /// <summary>Initializes a new instance of the <see cref="Test"/> class.</summary>
        public Test() =>
            SolutionTransforms.Add(static (solution, projectId) =>
            {
                var compilationOptions = solution.GetProject(projectId)!.CompilationOptions!;
                compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
                    compilationOptions.SpecificDiagnosticOptions.SetItems(CSharpVerifierHelper.NullableWarnings));
                return solution.WithProjectCompilationOptions(projectId, compilationOptions);
            });

        /// <summary>
        /// Runs the verification against LF sources, then — when a fixed state is being verified —
        /// converts every source to CRLF line endings and runs it again, so code fixes prove they
        /// honor the edited file's own line endings instead of hard-coding one form. Each pass pins
        /// end_of_line, which is where fix cleanup takes its newline from; without it the newline
        /// falls back to the host's, so the same fix emits CRLF on Windows and LF elsewhere.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public new async Task RunAsync(CancellationToken cancellationToken)
        {
            var verifiesFixedSources = FixedState.Sources.Count > 0 && AnySourceHasLineBreak(TestState.Sources);
            PinNewline(LineFeedConfig);

            await base.RunAsync(cancellationToken).ConfigureAwait(false);
            if (!verifiesFixedSources)
            {
                return;
            }

            ConvertSourcesToCrlf(TestState.Sources);
            ConvertSourcesToCrlf(FixedState.Sources);
            ConvertSourcesToCrlf(BatchFixedState.Sources);
            PinNewline(CarriageReturnLineFeedConfig);
            await base.RunAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Adds the nested config to one state, replacing the content set by an earlier pass.</summary>
        /// <param name="configs">The state's analyzer config files.</param>
        /// <param name="config">The analyzer config content.</param>
        private static void SetNestedEditorConfig(SourceFileCollection configs, string config)
        {
            for (var i = 0; i < configs.Count; i++)
            {
                var (filename, content) = configs[i];
                if (!string.Equals(filename, NestedEditorConfigPath, StringComparison.Ordinal))
                {
                    continue;
                }

                configs[i] = (filename, Microsoft.CodeAnalysis.Text.SourceText.From(config, content.Encoding));
                return;
            }

            configs.Add((NestedEditorConfigPath, config));
        }

        /// <summary>Returns whether any source carries a line break the CRLF variant could exercise.</summary>
        /// <param name="sources">The state's source list.</param>
        /// <returns><see langword="true"/> when a line break exists.</returns>
        private static bool AnySourceHasLineBreak(SourceFileList sources)
        {
            for (var i = 0; i < sources.Count; i++)
            {
                if (sources[i].content.ToString().Contains('\n', StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Rewrites every source in a state to CRLF line endings.</summary>
        /// <param name="sources">The state's source list.</param>
        private static void ConvertSourcesToCrlf(SourceFileList sources)
        {
            for (var i = 0; i < sources.Count; i++)
            {
                var (name, content) = sources[i];
                var text = content.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
                sources[i] = (name, Microsoft.CodeAnalysis.Text.SourceText.From(text, content.Encoding));
            }
        }

        /// <summary>Pins the newline on every state that carries analyzer config files of its own.</summary>
        /// <param name="config">The analyzer config content.</param>
        private void PinNewline(string config)
        {
            SetNestedEditorConfig(TestState.AnalyzerConfigFiles, config);

            // A state with no config files of its own inherits the ones above, so adding here would
            // stop that inheritance rather than extend it.
            if (FixedState.AnalyzerConfigFiles.Count > 0)
            {
                SetNestedEditorConfig(FixedState.AnalyzerConfigFiles, config);
            }

            if (BatchFixedState.AnalyzerConfigFiles.Count == 0)
            {
                return;
            }

            SetNestedEditorConfig(BatchFixedState.AnalyzerConfigFiles, config);
        }
    }
}
