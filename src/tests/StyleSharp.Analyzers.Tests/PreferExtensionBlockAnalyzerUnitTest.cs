// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using VerifyPreferExtension = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.PreferExtensionBlockAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the prefer-extension-block rule (SST1703, opt-in).</summary>
public class PreferExtensionBlockAnalyzerUnitTest
{
    /// <summary>Verifies a classic this-parameter extension method is reported (SST1703) on C# 14.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassicExtensionMethodReportedAsync()
        => await RunAsync(
            """
            public static class Ext
            {
                public static bool {|SST1703:IsBlank|}(this string text) => text.Length == 0;
            }
            """);

    /// <summary>Verifies a non-extension static method is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonExtensionMethodIsCleanAsync()
        => await RunAsync(
            """
            public static class Ext
            {
                public static bool IsBlank(string text) => text.Length == 0;
            }
            """);

    /// <summary>Runs the analyzer verifier with the language version set to one that supports extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source)
    {
        var test = new VerifyPreferExtension.Test
        {
            TestCode = source,
        };

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
