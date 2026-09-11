// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using VerifyGoto = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2014AvoidGotoAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2014 (avoid goto).</summary>
public class AvoidGotoAnalyzerUnitTest
{
    /// <summary>A jump out of a loop, the shape a labelled break replaces from C# 15.</summary>
    private const string LoopEscape = """
                                      public class C
                                      {
                                          public int M(int[] values)
                                          {
                                              for (var i = 0; i < values.Length; i++)
                                              {
                                                  if (values[i] < 0)
                                                  {
                                                      {|SST2014:goto Failed;|}
                                                  }
                                              }

                                              return 0;

                                          Failed:
                                              return -1;
                                          }
                                      }
                                      """;

    /// <summary>Verifies a jump out of a loop is reported before C# 15, where nothing else expresses it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task JumpToALabelIsReportedAsync() =>
        RunAsync(LoopEscape, LanguageVersion.CSharp13);

    /// <summary>Verifies a jump out of a loop is left alone from C# 15, where a labelled break says it directly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoopEscapeIsCleanOnCSharp15Async() =>
        RunAsync(LoopEscape.Replace("{|SST2014:goto Failed;|}", "goto Failed;", StringComparison.Ordinal));

    /// <summary>Verifies a jump with no enclosing loop is still reported on C# 15.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task JumpOutsideALoopIsStillReportedAsync() =>
        RunAsync(
            """
            public class C
            {
                public int M(int value)
                {
                    if (value < 0)
                    {
                        {|SST2014:goto Failed;|}
                    }

                    return 0;

                Failed:
                    return -1;
                }
            }
            """);

    /// <summary>Verifies a jump to a label inside the same loop is still reported on C# 15.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task JumpWithinALoopIsStillReportedAsync() =>
        RunAsync(
            """
            public class C
            {
                public int M(int[] values)
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                    Retry:
                        if (values[i] < 0)
                        {
                            values[i] = -values[i];
                            {|SST2014:goto Retry;|}
                        }
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a jump between switch sections is not reported: the language has no other word for it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task JumpBetweenSwitchSectionsIsCleanAsync() =>
        RunAsync(
            """
            public class C
            {
                public int M(int state)
                {
                    switch (state)
                    {
                        case 1:
                            System.Console.WriteLine(1);
                            goto case 2;

                        case 2:
                            System.Console.WriteLine(2);
                            goto default;

                        default:
                            return 0;
                    }
                }
            }
            """);

    /// <summary>Verifies the structured jumps are never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StructuredJumpsAreCleanAsync() =>
        RunAsync(
            """
            public class C
            {
                public int M(int[] values)
                {
                    foreach (var value in values)
                    {
                        if (value == 0)
                        {
                            continue;
                        }

                        if (value < 0)
                        {
                            break;
                        }

                        return value;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Runs the analyzer verifier at the requested language version.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="languageVersion">The language version to parse with.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, LanguageVersion languageVersion = LanguageVersion.Preview)
    {
        var test = new VerifyGoto.Test { TestCode = source };

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(languageVersion));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
