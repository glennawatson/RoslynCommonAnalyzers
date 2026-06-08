// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Regression tests pinning the modern-C# idioms that StyleCop.Analyzers has open false-positive
/// bugs for. Each asserts that the StyleSharp counterpart stays silent, guarding the "correct on
/// modern C#" guarantee. Issue numbers reference the StyleCopAnalyzers tracker.
/// </summary>
public class ModernCSharpRegressionTests
{
    /// <summary>A discard parameter '_' is not a naming violation (cf. StyleCop #2974).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscardParameterIsCleanAsync()
        => await CSharpAnalyzerVerifier<Sst1313ParameterNamingAnalyzer>.VerifyAnalyzerAsync(
            "public class C { public void M(int _, int __) { } }");

    /// <summary>A discard local '_' is not a naming violation (cf. StyleCop #3057).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscardLocalIsCleanAsync()
        => await CSharpAnalyzerVerifier<Sst1312LocalVariableNamingAnalyzer>.VerifyAnalyzerAsync(
            "public class C { public void M() { _ = System.Math.Abs(-1); } }");

    /// <summary>A multi-line collection expression does not trip element indentation (cf. StyleCop #3904).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CollectionExpressionIndentationIsCleanAsync()
        => await CSharpAnalyzerVerifier<Sst1137ElementIndentationAnalyzer>.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static readonly int[] Values =
                [
                    1,
                    2,
                    3,
                ];
            }
            """);

    /// <summary>A using directive with a file-scoped namespace keeps its placement clean (cf. StyleCop #3578).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileScopedNamespaceUsingPlacementIsCleanAsync()
        => await CSharpAnalyzerVerifier<UsingOrderingAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;

            namespace MyApp;

            public class C
            {
                public DateTime Now => DateTime.Now;
            }
            """);

    /// <summary>A tuple deconstruction discard is not a tuple-element naming violation (cf. StyleCop #3878).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeconstructionDiscardIsCleanAsync()
    {
        var test = new CSharpAnalyzerVerifier<Sst1316TupleElementNamingAnalyzer>.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = "public class C { public void M() { var (a, _) = (1, 2); System.Console.WriteLine(a); } }"
        };

        await test.RunAsync(CancellationToken.None);
    }
}
