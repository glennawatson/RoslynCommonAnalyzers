// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for thread-sleep-in-test analyzer benchmarks (SST2506).</summary>
internal static class ThreadSleepInTestBenchmarkSource
{
    /// <summary>Builds a compilation unit whose test methods either wait cleanly or sleep on the wall clock.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using Xunit;

           namespace Xunit { public sealed class FactAttribute : System.Attribute { } }

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose test method waits without a fixed delay.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               [Fact]
               public void Test()
               {
                   System.GC.KeepAlive(this);
               }
           }
           """;

    /// <summary>Builds one type whose test method sleeps for a fixed real-time delay.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               [Fact]
               public void Test()
               {
                   System.Threading.Thread.Sleep(100);
               }
           }
           """;
}
