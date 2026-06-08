// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Micro-benchmarks for the SST1110 opening-token declaration-line check.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class OpeningParenOnDeclarationLineBenchmarks
{
    /// <summary>The source text for the selected scenario.</summary>
    private SourceText _text = null!;

    /// <summary>The opening token for the selected scenario.</summary>
    private SyntaxToken _openingToken;

    /// <summary>The line containing the opening token.</summary>
    private int _openingLine;

    /// <summary>The benchmark scenarios.</summary>
    public enum Scenario
    {
        /// <summary>A normal declaration-line opening token.</summary>
        Clean,

        /// <summary>An opening token that has moved onto the next line.</summary>
        Violating,

        /// <summary>A parenthesized callback lambda on the next argument line.</summary>
        ParenthesizedLambda
    }

    /// <summary>Gets or sets the scenario under test.</summary>
    [Params(Scenario.Clean, Scenario.Violating, Scenario.ParenthesizedLambda)]
    public Scenario CurrentScenario { get; set; }

    /// <summary>Builds the token fixture for the selected scenario.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        switch (CurrentScenario)
        {
            case Scenario.Clean:
                {
                    var method = ParseSingleMethod("class C { void M(int x) { } }");
                    _openingToken = method.ParameterList.OpenParenToken;
                    break;
                }

            case Scenario.Violating:
                {
                    var method = ParseSingleMethod(
                        """
                        class C
                        {
                            void M
                                (int x) { }
                        }
                        """);
                    _openingToken = method.ParameterList.OpenParenToken;
                    break;
                }

            default:
                {
                    var method = ParseSingleMethod(
                        """
                        using System;

                        class C
                        {
                            void M()
                            {
                                Run(value => { },
                                    () =>
                                    {
                                    });
                            }

                            void Run(Action<int> onNext, Action onCompleted)
                            {
                            }
                        }
                        """);
                    var invocation = (InvocationExpressionSyntax)((ExpressionStatementSyntax)method.Body!.Statements[0]).Expression;
                    _openingToken = ((ParenthesizedLambdaExpressionSyntax)invocation.ArgumentList.Arguments[1].Expression).ParameterList.OpenParenToken;
                    break;
                }
        }

        _text = await _openingToken.SyntaxTree!.GetTextAsync().ConfigureAwait(false);
        _openingLine = LayoutHelpers.StartLine(_text, _openingToken);
    }

    /// <summary>Benchmarks the SST1110 opening-line predicate.</summary>
    /// <returns><see langword="true"/> when the opening token is treated as on the declaration line.</returns>
    [Benchmark]
    public bool IsOpeningOnDeclarationLine()
        => ParameterListLayoutAnalyzer.IsOpeningOnDeclarationLine(_text, _openingToken, _openingLine);

    /// <summary>Parses the single method declaration from a single-type snippet.</summary>
    /// <param name="source">The source to parse.</param>
    /// <returns>The parsed method declaration.</returns>
    private static MethodDeclarationSyntax ParseSingleMethod(string source)
        => (MethodDeclarationSyntax)((ClassDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[^1]).Members[0];
}
