// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using VerifyIndexer = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1710PreferExtensionIndexerAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the prefer-extension-indexer rule (SST1710, opt-in).</summary>
public class Sst1710PreferExtensionIndexerAnalyzerUnitTest
{
    /// <summary>Verifies an accessor-shaped extension method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AccessorShapedExtensionReportedAsync() =>
        RunAsync(
            """
            public static class Ext
            {
                public static int {|SST1710:ElementAt|}(this int[] source, int index) => source[index];
            }
            """);

    /// <summary>Verifies a string-keyed lookup extension is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StringKeyedAccessorReportedAsync() =>
        RunAsync(
            """
            public static class Ext
            {
                public static int {|SST1710:Get|}(this int[] source, string key) => source.Length + key.Length;
            }
            """);

    /// <summary>Verifies a method that returns nothing is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task VoidReturnIsCleanAsync() =>
        RunAsync(
            """
            public static class Ext
            {
                public static void Get(this int[] source, int index) => source[index].ToString();
            }
            """);

    /// <summary>Verifies a method taking more than one index parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExtraParameterIsCleanAsync() =>
        RunAsync(
            """
            public static class Ext
            {
                public static int Get(this int[] source, int index, int fallback) => index < source.Length ? source[index] : fallback;
            }
            """);

    /// <summary>Verifies a method whose name does not read as an accessor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonAccessorNameIsCleanAsync() =>
        RunAsync(
            """
            public static class Ext
            {
                public static int Scaled(this int[] source, int factor) => source.Length * factor;
            }
            """);

    /// <summary>Verifies a plain static method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonExtensionIsCleanAsync() =>
        RunAsync(
            """
            public static class Ext
            {
                public static int Get(int[] source, int index) => source[index];
            }
            """);

    /// <summary>Verifies nothing is reported below C# 15, where extension indexers do not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BelowCSharp15IsCleanAsync() =>
        RunAsync(
            """
            public static class Ext
            {
                public static int ElementAt(this int[] source, int index) => source[index];
            }
            """,
            LanguageVersion.CSharp13);

    /// <summary>Runs the analyzer verifier at the requested language version.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="languageVersion">The language version to parse with.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, LanguageVersion languageVersion = LanguageVersion.Preview)
    {
        var test = new VerifyIndexer.Test { TestCode = source };

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(languageVersion));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
