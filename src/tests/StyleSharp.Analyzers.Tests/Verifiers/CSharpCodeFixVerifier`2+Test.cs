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
        public Test() =>
            SolutionTransforms.Add((solution, projectId) =>
            {
                var compilationOptions = solution.GetProject(projectId)!.CompilationOptions!;
                compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
                    compilationOptions.SpecificDiagnosticOptions.SetItems(CSharpVerifierHelper.NullableWarnings));
                return solution.WithProjectCompilationOptions(projectId, compilationOptions);
            });
    }
}
