// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Blazor.Common.Analyzers.Tests;

/// <summary>
/// Provides helpers for verifying the behaviour of a C# <see cref="DiagnosticAnalyzer"/> in tests.
/// </summary>
/// <typeparam name="TAnalyzer">The type of the analyzer under test.</typeparam>
public static partial class CSharpAnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.Diagnostic()"/>
    /// <returns>A new <see cref="DiagnosticResult"/> for the analyzer's single supported diagnostic.</returns>
    public static DiagnosticResult Diagnostic()
        => CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic();

    /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.Diagnostic(string)"/>
    /// <param name="diagnosticId">The diagnostic identifier to expect.</param>
    /// <returns>A new <see cref="DiagnosticResult"/> for the specified diagnostic identifier.</returns>
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic(diagnosticId);

    /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.Diagnostic(DiagnosticDescriptor)"/>
    /// <param name="descriptor">The descriptor of the diagnostic to expect.</param>
    /// <returns>A new <see cref="DiagnosticResult"/> for the specified diagnostic descriptor.</returns>
    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
        => CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic(descriptor);

    /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.VerifyAnalyzerAsync(string, DiagnosticResult[])"/>
    /// <param name="source">The source code to analyze.</param>
    /// <param name="expected">The diagnostics expected to be produced for the source.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous verification operation.</returns>
    public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new Test
        {
            TestCode = source,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }
}
