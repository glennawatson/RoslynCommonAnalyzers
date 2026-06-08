// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace StyleSharp.Analyzers.Tests;

/// <content>
/// Contains the nested <see cref="Test"/> type used to run C# code refactoring verification.
/// </content>
public static partial class CSharpCodeRefactoringVerifier<TCodeRefactoring>
    where TCodeRefactoring : CodeRefactoringProvider, new()
{
    /// <summary>
    /// A configured C# code refactoring test that enables nullable reference type warnings during validation.
    /// </summary>
    public class Test : CSharpCodeRefactoringTest<TCodeRefactoring, DefaultVerifier>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Test"/> class.
        /// </summary>
        public Test() => SolutionTransforms.Add(CSharpVerifierHelper.ConfigureSolution);

        /// <inheritdoc/>
        protected override async Task RunImplAsync(CancellationToken cancellationToken)
        {
            // Normalize snippets to \n so the test and its expected output never depend on checkout line endings.
            CSharpVerifierHelper.NormalizeLineEndings(TestState.Sources);
            CSharpVerifierHelper.NormalizeLineEndings(FixedState.Sources);
            await base.RunImplAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
