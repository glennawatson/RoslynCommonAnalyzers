// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for namespace-folder analyzer benchmarks.</summary>
internal static class NamespaceFolderBenchmarkSource
{
    /// <summary>The benchmark file path used to derive the expected namespace.</summary>
    public const string FilePath = "/src/MyApp/Models/Widget.cs";

    /// <summary>Builds a compilation unit containing many top-level namespaces.</summary>
    /// <param name="members">The number of namespace blocks to emit.</param>
    /// <param name="violating">Whether to emit a namespace that does not match the folder path.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
    {
        var namespaceName = violating ? "MyApp.Wrong" : "MyApp.Models";
        return BenchmarkSourceText.JoinBlocks(
            members,
            i => $$"""
                  namespace {{namespaceName}}
                  {
                      internal sealed class Widget{{i}}
                      {
                      }
                  }
                  """);
    }
}
