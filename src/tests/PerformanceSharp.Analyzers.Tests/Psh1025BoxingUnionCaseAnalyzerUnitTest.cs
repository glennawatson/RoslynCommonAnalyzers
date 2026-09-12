// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if ROSLYN_5_9_OR_GREATER

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using VerifyBoxingUnionCase = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1025BoxingUnionCaseAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1025 (a value-typed union case boxes into the union's payload).</summary>
/// <remarks>
/// Compiled only against the slot whose compiler can parse a union declaration; the lower slots cannot
/// parse the syntax, and a host on those slots cannot compile a union either.
/// </remarks>
public class Psh1025BoxingUnionCaseAnalyzerUnitTest
{
    /// <summary>The stand-in runtime support a union declaration binds against.</summary>
    private const string Marker = """
                                  #nullable enable
                                  namespace System.Runtime.CompilerServices
                                  {
                                      public interface IUnion { }

                                      [System.AttributeUsage(System.AttributeTargets.All)]
                                      public sealed class UnionAttribute : System.Attribute { }
                                  }

                                  """;

    /// <summary>Verifies a value-typed case is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ValueTypeCaseIsReportedAsync() =>
        RunAsync(Marker + """
            public union Reading({|PSH1025:int|}, {|PSH1025:double|});
            """);

    /// <summary>Verifies a reference-typed case is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReferenceTypeCaseIsCleanAsync() =>
        RunAsync(Marker + """
            public union Payload(string, byte[]);
            """);

    /// <summary>Verifies only the value-typed case of a mixed union is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MixedCasesReportOnlyTheValueTypeAsync() =>
        RunAsync(Marker + """
            public union Payload(string, {|PSH1025:int|});
            """);

    /// <summary>Runs the analyzer verifier against the C# version that has unions.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source)
    {
        var test = new VerifyBoxingUnionCase.Test { TestCode = source };

        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });

        await test.RunAsync(CancellationToken.None);
    }
}

#endif
