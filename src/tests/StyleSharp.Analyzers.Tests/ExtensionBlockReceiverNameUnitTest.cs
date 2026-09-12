// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;

using VerifyReceiverName = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1712UnusableReceiverNameAnalyzer>;
using VerifyReceiverNameFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1712UnusableReceiverNameAnalyzer,
    StyleSharp.Analyzers.Sst1712UnusableReceiverNameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the unusable extension-block receiver name rule (SST1712).</summary>
public class ExtensionBlockReceiverNameUnitTest
{
    /// <summary>Verifies a block of only static members reports its receiver name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticOnlyBlockReportsItsReceiverNameAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                extension(string {|SST1712:text|})
                {
                    public static string Blank => "   ";

                    public static string Repeat(char value) => new(value, 3);
                }
            }
            """);

    /// <summary>Verifies the nameless receiver form is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamelessReceiverIsCleanAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                extension(string)
                {
                    public static string Blank => "   ";
                }
            }
            """);

    /// <summary>Verifies a block holding any instance member keeps its receiver name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BlockWithAnInstanceMemberIsCleanAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                extension(string text)
                {
                    public static string Blank => "   ";

                    public int Doubled => text.Length * 2;
                }
            }
            """);

    /// <summary>Verifies an empty block is left to the empty-block rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>An empty block names an unusable receiver too, but SST1700 is the rule that reports it.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmptyBlockIsCleanAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                extension(string text)
                {
                }
            }
            """);

    /// <summary>Verifies the fix drops the receiver name and leaves the type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixDropsTheReceiverNameAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  extension(string {|SST1712:text|})
                                  {
                                      public static string Blank => "   ";
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public static class StringExtensions
                                   {
                                       extension(string)
                                       {
                                           public static string Blank => "   ";
                                       }
                                   }
                                   """;
        var test = new VerifyReceiverNameFix.Test { TestCode = Source, FixedCode = FixedSource };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the verifier at a language version that has extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source)
    {
        var test = new VerifyReceiverName.Test { TestCode = source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
