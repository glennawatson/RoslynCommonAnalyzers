// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if ROSLYN_5_9_OR_GREATER
using System.Collections.Immutable;
#endif
using System.Runtime.CompilerServices;
#if ROSLYN_5_9_OR_GREATER
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
#endif
using VerifyBoxingUnionCase = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1025BoxingUnionCaseAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1025 (a value-typed union case boxes into the union's payload).</summary>
/// <remarks>
/// Union cases run against the slot whose compiler can parse a union declaration. Lower slots verify
/// initialization without unsupported syntax registration.
/// </remarks>
public class Psh1025BoxingUnionCaseAnalyzerUnitTest
{
#if ROSLYN_5_9_OR_GREATER
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

    /// <summary>Verifies named structs are reported while named classes and object remain clean.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NamedCasesUseTheirBoundValueTypeAsync() =>
        RunAsync(Marker + """
            public struct Reading { }
            public class Message { }
            public union Payload({|PSH1025:Reading|}, Message, object);
            """);

    /// <summary>Verifies incomplete declarations and unresolved case types do not produce boxing reports.</summary>
    /// <param name="declaration">The incomplete union declaration.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public union Payload;")]
    [Arguments("public union Payload();")]
    [Arguments("public union Payload(Missing);")]
    [Arguments("public union Payload(,);")]
    [Arguments("public union Payload(__arglist);")]
    public async Task IncompleteUnionCasesAreCleanAsync(string declaration, CancellationToken cancellationToken)
    {
        var diagnostics = await AnalyzeAsync(Marker + declaration, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

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

    /// <summary>Analyzes union syntax even when its declaration is not yet complete.</summary>
    /// <param name="source">The source with experimental union syntax.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The analyzer diagnostics.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, CancellationToken cancellationToken) =>
        CSharpCompilation.Create(
                "UnionDeclarationTests",
                [CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), cancellationToken: cancellationToken)],
                RuntimeMetadataReferences.Platform,
                new(OutputKind.DynamicallyLinkedLibrary))
            .WithAnalyzers([new Psh1025BoxingUnionCaseAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
#else
    /// <summary>Verifies the older compiler slot initializes without registering unsupported union syntax.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CompilerWithoutUnionSyntaxIsCleanAsync() =>
        VerifyBoxingUnionCase.VerifyAnalyzerAsync("public class C { public int M() => 1; }");
#endif
}
