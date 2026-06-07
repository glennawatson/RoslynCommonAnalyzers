// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.VisualBasic.Testing;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Provides helpers for verifying the behaviour of a Visual Basic <see cref="DiagnosticAnalyzer"/> and its associated <see cref="CodeFixProvider"/> in tests.
/// </summary>
/// <typeparam name="TAnalyzer">The type of the analyzer under test.</typeparam>
/// <typeparam name="TCodeFix">The type of the code fix provider under test.</typeparam>
public static partial class VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.Diagnostic()"/>
    /// <returns>A new <see cref="DiagnosticResult"/> for the analyzer's single supported diagnostic.</returns>
    public static DiagnosticResult Diagnostic()
        => VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic();

    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.Diagnostic(string)"/>
    /// <param name="diagnosticId">The diagnostic identifier to expect.</param>
    /// <returns>A new <see cref="DiagnosticResult"/> for the specified diagnostic identifier.</returns>
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(diagnosticId);

    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.Diagnostic(DiagnosticDescriptor)"/>
    /// <param name="descriptor">The descriptor of the diagnostic to expect.</param>
    /// <returns>A new <see cref="DiagnosticResult"/> for the specified diagnostic descriptor.</returns>
    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
        => VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(descriptor);

    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyAnalyzerAsync(string, DiagnosticResult[])"/>
    /// <param name="source">The source code to analyze.</param>
    /// <param name="expected">The diagnostics expected to be produced for the source.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous verification operation.</returns>
    public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new Test
        {
            TestCode = source
        };

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyCodeFixAsync(string, string)"/>
    /// <param name="source">The source code to analyze and fix.</param>
    /// <param name="fixedSource">The expected source code after the fix is applied.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous verification operation.</returns>
    public static async Task VerifyCodeFixAsync(string source, string fixedSource)
        => await VerifyCodeFixAsync(source, DiagnosticResult.EmptyDiagnosticResults, fixedSource);

    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyCodeFixAsync(string, DiagnosticResult, string)"/>
    /// <param name="source">The source code to analyze and fix.</param>
    /// <param name="expected">The diagnostic expected to be produced for the source.</param>
    /// <param name="fixedSource">The expected source code after the fix is applied.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous verification operation.</returns>
    public static async Task VerifyCodeFixAsync(string source, DiagnosticResult expected, string fixedSource)
        => await VerifyCodeFixAsync(source, [expected], fixedSource);

    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyCodeFixAsync(string, DiagnosticResult[], string)"/>
    /// <param name="source">The source code to analyze and fix.</param>
    /// <param name="expected">The diagnostics expected to be produced for the source.</param>
    /// <param name="fixedSource">The expected source code after the fix is applied.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous verification operation.</returns>
    public static async Task VerifyCodeFixAsync(string source, DiagnosticResult[] expected, string fixedSource)
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
