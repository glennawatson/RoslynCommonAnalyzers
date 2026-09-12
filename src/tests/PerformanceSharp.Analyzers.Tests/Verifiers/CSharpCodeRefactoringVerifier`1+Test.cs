// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <content>
/// Contains the nested <see cref="Test"/> type used to run C# code refactoring verification.
/// </content>
public static partial class CSharpCodeRefactoringVerifier<TCodeRefactoring>
    where TCodeRefactoring : CodeRefactoringProvider, new()
{
    /// <summary>A configured C# code refactoring test that enables nullable reference type warnings during validation.</summary>
    public class Test : CSharpCodeRefactoringTest<TCodeRefactoring, DefaultVerifier>
    {
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
        /// converts every source to CRLF line endings and runs it again, so refactorings prove they
        /// honor the edited file's own line endings instead of hard-coding one form.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public new async Task RunAsync(CancellationToken cancellationToken)
        {
            var verifiesFixedSources = FixedState.Sources.Count > 0
                && TestSourceLineEndings.AnySourceHasLineBreak(TestState.Sources);

            PinNewline(TestSourceLineEndings.LineFeedConfig);
            await base.RunAsync(cancellationToken).ConfigureAwait(false);
            if (!verifiesFixedSources)
            {
                return;
            }

            TestSourceLineEndings.ConvertSourcesToCrlf(TestState.Sources);
            TestSourceLineEndings.ConvertSourcesToCrlf(FixedState.Sources);
            PinNewline(TestSourceLineEndings.CarriageReturnLineFeedConfig);
            await base.RunAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Pins the newline on every state that carries analyzer config files of its own.</summary>
        /// <param name="config">The analyzer config content.</param>
        private void PinNewline(string config)
        {
            TestSourceLineEndings.SetNestedEditorConfig(TestState.AnalyzerConfigFiles, config);

            // A state with no config files of its own inherits the ones above, so adding here would
            // stop that inheritance rather than extend it.
            if (FixedState.AnalyzerConfigFiles.Count == 0)
            {
                return;
            }

            TestSourceLineEndings.SetNestedEditorConfig(FixedState.AnalyzerConfigFiles, config);
        }
    }
}
