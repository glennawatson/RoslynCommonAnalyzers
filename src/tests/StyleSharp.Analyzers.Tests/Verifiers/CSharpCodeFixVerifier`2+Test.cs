// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace StyleSharp.Analyzers.Tests;

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
        public Test() => SolutionTransforms.Add(CSharpVerifierHelper.ConfigureSolution);

        /// <inheritdoc/>
        protected override async Task RunImplAsync(CancellationToken cancellationToken)
        {
            // Normalize every snippet to \n so the test (and its expected fix output) never depends on the line
            // endings git checked the file out with. Done centrally here so it also covers tests that build the
            // Test directly instead of going through the VerifyCodeFixAsync helpers.
            CSharpVerifierHelper.NormalizeLineEndings(TestState.Sources);
            CSharpVerifierHelper.NormalizeLineEndings(FixedState.Sources);
            CSharpVerifierHelper.NormalizeLineEndings(BatchFixedState.Sources);
            await base.RunImplAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
