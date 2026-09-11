// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyInParameterFix = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1007PassLargeReadonlyStructByInAnalyzer,
    PerformanceSharp.Analyzers.Psh1007PassLargeReadonlyStructByInCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for the PSH1007 code fix (pass large readonly structs by 'in' reference).</summary>
public class Psh1007PassLargeReadonlyStructByInCodeFixUnitTest
{
    /// <summary>The struct the tests measure against, at 40 bytes.</summary>
    private const string Structs = """

        public readonly struct Snapshot
        {
            public readonly long A;
            public readonly long B;
            public readonly long C;
            public readonly long D;
            public readonly long E;
        }
        """;

    /// <summary>Verifies the fix adds the modifier ahead of the parameter type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterGainsTheInModifierAsync()
    {
        const string Source = """
                              internal static class C
                              {
                                  internal static long Score(Snapshot {|PSH1007:snapshot|}) => snapshot.A;
                              }
                              """;
        const string FixedSource = """
                                   internal static class C
                                   {
                                       internal static long Score(in Snapshot snapshot) => snapshot.A;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a parameter on its own line keeps its indentation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WrappedParameterKeepsItsIndentationAsync()
    {
        const string Source = """
                              internal static class C
                              {
                                  internal static long Score(
                                      Snapshot {|PSH1007:snapshot|},
                                      long scale) => snapshot.A * scale;
                              }
                              """;
        const string FixedSource = """
                                   internal static class C
                                   {
                                       internal static long Score(
                                           in Snapshot snapshot,
                                           long scale) => snapshot.A * scale;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Runs a code-fix verification with the shared struct declarations appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource)
    {
        var test = new VerifyInParameterFix.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source + Structs, FixedCode = fixedSource + Structs, };

        await test.RunAsync(CancellationToken.None);
    }
}
