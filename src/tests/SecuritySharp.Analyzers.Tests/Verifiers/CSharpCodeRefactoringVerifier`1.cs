// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Testing;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>
/// Provides helpers for verifying the behaviour of a C# <see cref="CodeRefactoringProvider"/> in tests.
/// </summary>
/// <typeparam name="TCodeRefactoring">The type of the code refactoring provider under test.</typeparam>
public static partial class CSharpCodeRefactoringVerifier<TCodeRefactoring>
    where TCodeRefactoring : CodeRefactoringProvider, new()
{
    /// <inheritdoc cref="CodeRefactoringVerifier{TCodeRefactoring, TTest, TVerifier}.VerifyRefactoringAsync(string, string)"/>
    /// <param name="source">The source code to refactor.</param>
    /// <param name="fixedSource">The expected source code after the refactoring is applied.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous verification operation.</returns>
    public static async Task VerifyRefactoringAsync(string source, string fixedSource) => await VerifyRefactoringAsync(source, DiagnosticResult.EmptyDiagnosticResults, fixedSource);

    /// <inheritdoc cref="CodeRefactoringVerifier{TCodeRefactoring, TTest, TVerifier}.VerifyRefactoringAsync(string, DiagnosticResult, string)"/>
    /// <param name="source">The source code to refactor.</param>
    /// <param name="expected">The diagnostic expected to be produced for the source.</param>
    /// <param name="fixedSource">The expected source code after the refactoring is applied.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous verification operation.</returns>
    public static async Task VerifyRefactoringAsync(string source, DiagnosticResult expected, string fixedSource) => await VerifyRefactoringAsync(source, [expected], fixedSource);

    /// <inheritdoc cref="CodeRefactoringVerifier{TCodeRefactoring, TTest, TVerifier}.VerifyRefactoringAsync(string, DiagnosticResult[], string)"/>
    /// <param name="source">The source code to refactor.</param>
    /// <param name="expected">The diagnostics expected to be produced for the source.</param>
    /// <param name="fixedSource">The expected source code after the refactoring is applied.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous verification operation.</returns>
    public static async Task VerifyRefactoringAsync(string source, DiagnosticResult[] expected, string fixedSource)
    {
        var test = new Test
        {
            TestCode = source,
            FixedCode = fixedSource
        };

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }
}
