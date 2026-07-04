// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
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
    /// <summary>
    /// A configured C# code fix test that enables nullable reference type warnings during validation.
    /// </summary>
    public class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Test"/> class.
        /// </summary>
        public Test() =>
            SolutionTransforms.Add(static (solution, projectId) =>
            {
                var compilationOptions = solution.GetProject(projectId)!.CompilationOptions!;
                compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
                    compilationOptions.SpecificDiagnosticOptions.SetItems(CSharpVerifierHelper.NullableWarnings));
                return solution.WithProjectCompilationOptions(projectId, compilationOptions);
            });

        /// <summary>
        /// Runs the verification, then — when a fixed state is being verified — converts every
        /// source to CRLF line endings and runs it again, so code fixes prove they honor the
        /// edited file's own line endings instead of hard-coding one form.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public new async Task RunAsync(CancellationToken cancellationToken)
        {
            await base.RunAsync(cancellationToken).ConfigureAwait(false);
            if (FixedState.Sources.Count == 0 || !AnySourceHasLineBreak(TestState.Sources))
            {
                return;
            }

            ConvertSourcesToCrlf(TestState.Sources);
            ConvertSourcesToCrlf(FixedState.Sources);
            ConvertSourcesToCrlf(BatchFixedState.Sources);

            // A real CRLF repo pins end_of_line, which is where fix cleanup takes its newline
            // from; the nested config leaves each test's own "/.editorconfig" untouched.
            const string CrlfConfig = "[*]\nend_of_line = crlf\n";
            TestState.AnalyzerConfigFiles.Add(("/0/.editorconfig", CrlfConfig));
            if (FixedState.AnalyzerConfigFiles.Count > 0)
            {
                FixedState.AnalyzerConfigFiles.Add(("/0/.editorconfig", CrlfConfig));
            }

            if (BatchFixedState.AnalyzerConfigFiles.Count > 0)
            {
                BatchFixedState.AnalyzerConfigFiles.Add(("/0/.editorconfig", CrlfConfig));
            }

            await base.RunAsync(cancellationToken).ConfigureAwait(false);
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
    }
}
