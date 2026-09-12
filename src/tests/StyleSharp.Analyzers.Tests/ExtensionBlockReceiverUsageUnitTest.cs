// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;

using VerifyBlockReceiver = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1711UnusedBlockReceiverAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the unused extension-block receiver rule (SST1711).</summary>
public class ExtensionBlockReceiverUsageUnitTest
{
    /// <summary>Verifies a property that never reads the receiver is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PropertyIgnoringTheReceiverIsReportedAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                extension(string text)
                {
                    public int {|SST1711:Always|} => 42;
                }
            }
            """);

    /// <summary>Verifies a method that never reads the receiver is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MethodIgnoringTheReceiverIsReportedAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                extension(string text)
                {
                    public int {|SST1711:Seven|}() => 7;
                }
            }
            """);

    /// <summary>Verifies a member that reads the receiver in any accessor is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The setter alone reading the receiver is enough; the pair is one member.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MemberReadingTheReceiverIsCleanAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                extension(string text)
                {
                    public int Length => text.Length;

                    public int Doubled() => text.Length * 2;
                }
            }
            """);

    /// <summary>Verifies a static block member is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The receiver is not in scope for a static extension member, so it could not read it either way.
    /// Reporting one would make every static extension member a violation.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticBlockMemberIsCleanAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                extension(string text)
                {
                    public static string Blank => "   ";
                }
            }
            """);

    /// <summary>Verifies a member outside an extension block is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PlainStaticHelperIsCleanAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                public static int Always() => 42;
            }
            """);

    /// <summary>Runs the verifier at a language version that has extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source)
    {
        var test = new VerifyBlockReceiver.Test { TestCode = source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
