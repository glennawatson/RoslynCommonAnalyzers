// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using VerifyNameofLiteral = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1463NameofLiteralAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1463NameofLiteralAnalyzer"/>.</summary>
public class NameofLiteralAnalyzerUnitTest
{
    /// <summary>Verifies a name-shaped argument matching a property is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SymbolNameLiteralIsReportedAsync()
        => await VerifyNameofLiteral.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Count { get; set; }

                public void M() => Notify({|SST1463:"Count"|});

                private static void Notify(string propertyName)
                {
                }
            }
            """);

    /// <summary>Verifies ordinary message strings are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonNameParameterIsCleanAsync()
        => await VerifyNameofLiteral.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Count { get; set; }

                public void M() => Log("Count");

                private static void Log(string message)
                {
                }
            }
            """);

    /// <summary>Verifies the rule stays silent below C# 6, where nameof does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp6Async()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Count { get; set; }

                                  public void M()
                                  {
                                      Notify("Count");
                                  }

                                  private static void Notify(string propertyName)
                                  {
                                  }
                              }
                              """;
        var test = new VerifyNameofLiteral.Test { TestCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp5));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
