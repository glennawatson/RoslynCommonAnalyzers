// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace StyleSharp.Analyzers.Tests;

/// <content>
/// Contains the nested <see cref="Test"/> type used to run C# analyzer verification.
/// </content>
public static partial class CSharpAnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <summary>
    /// A configured C# analyzer test that enables nullable reference type warnings during validation.
    /// </summary>
    public class Test : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Test"/> class.
        /// </summary>
        public Test() => SolutionTransforms.Add(CSharpVerifierHelper.ConfigureSolution);

        /// <inheritdoc/>
        protected override async Task RunImplAsync(CancellationToken cancellationToken)
        {
            // Normalize snippets to \n so diagnostic markup offsets never depend on checkout line endings.
            CSharpVerifierHelper.NormalizeLineEndings(TestState.Sources);
            await base.RunImplAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
