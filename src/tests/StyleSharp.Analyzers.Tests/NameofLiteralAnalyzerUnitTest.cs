// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;

using VerifyNameofLiteral = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1463NameofLiteralAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1463NameofLiteralAnalyzer"/>.</summary>
public class NameofLiteralAnalyzerUnitTest
{
    /// <summary>Verifies a name-shaped argument matching a property is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SymbolNameLiteralIsReportedAsync() =>
        VerifyNameofLiteral.VerifyAnalyzerAsync(
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

    /// <summary>Verifies a literal inside the initializer of a local of that name is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// A local is in scope from its declarator onward, so the lookup finds it — but naming it inside its
    /// own initializer is CS0841, not a rename-safe reference.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LiteralInTheLocalsOwnInitializerIsCleanAsync() =>
        VerifyNameofLiteral.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M()
                {
                    var value = Find("value");
                    return value;
                }

                private static string Find(string elementName) => elementName;
            }
            """);

    /// <summary>Verifies a literal is left alone when a pattern later on the line declares that name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The designation comes after the literal, so naming it there is CS0841.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LiteralBeforeAPatternDeclaringThatNameIsCleanAsync() =>
        VerifyNameofLiteral.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M()
                {
                    if (Find("summary") is not { } summary)
                    {
                        return "";
                    }

                    return summary.ToString();
                }

                private static object Find(string elementName) => elementName;
            }
            """);

    /// <summary>Verifies ordinary message strings are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonNameParameterIsCleanAsync() =>
        VerifyNameofLiteral.VerifyAnalyzerAsync(
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

    /// <summary>Verifies a name matching only a generic type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The bare name of a generic type is not a name the language will take: where the only
    /// <c>Holder</c> in scope is <c>Holder&lt;T&gt;</c>, <c>nameof(Holder)</c> is CS0305, so suggesting it
    /// would trade a working string for a build error.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NameMatchingOnlyAGenericTypeIsNotReportedAsync() =>
        VerifyNameofLiteral.VerifyAnalyzerAsync(
            """
            public sealed class Holder<T>
            {
            }

            public sealed class C
            {
                public void Row(string name)
                {
                }

                public void M() => Row("Holder");
            }
            """);
}
