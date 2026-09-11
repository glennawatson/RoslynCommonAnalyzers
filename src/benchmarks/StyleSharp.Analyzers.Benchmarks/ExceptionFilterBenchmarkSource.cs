// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for hand-rolled exception filter analysis.</summary>
internal static class ExceptionFilterBenchmarkSource
{
    /// <summary>The number of catch shapes cycled by this source generator.</summary>
    private const int CatchShapeCount = 2;

    /// <summary>Builds clean or violating catch-block members.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reportable catch blocks.</param>
    /// <returns>The generated source text.</returns>
    internal static string Generate(int members, bool violating) =>
        $$"""
           namespace Bench;

           internal sealed class ExceptionFilterBench
           {
               private static bool ShouldKeep() => System.DateTime.Now.Ticks > 0;

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating) =>
        (index % CatchShapeCount, violating) switch
        {
            (0, true) => GenerateRethrowInElse(index),
            (1, true) => GenerateRethrowInGuard(index),
            (0, false) => GenerateMethodCallGuardedCatch(index),
            _ => GenerateWhenFilteredCatch(index)
        };

    /// <summary>Builds a catch whose if-else rethrows in the else, which a when filter replaces.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateRethrowInElse(int index) =>
        $$"""
        public int Filter{{index}}(bool flag)
        {
            try
            {
                return {{index}};
            }
            catch (System.Exception ex)
            {
                if (flag)
                {
                    return ex.Message.Length;
                }
                else
                {
                    throw;
                }
            }
        }
        """;

    /// <summary>Builds a catch whose leading guard rethrows, which a when filter replaces.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateRethrowInGuard(int index) =>
        $$"""
        public int Guard{{index}}(bool flag)
        {
            try
            {
                return {{index}};
            }
            catch (System.Exception ex)
            {
                if (!flag)
                {
                    throw;
                }

                return ex.Message.Length;
            }
        }
        """;

    /// <summary>Builds a catch whose guard calls a method, so it is exempt from the rewrite.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMethodCallGuardedCatch(int index) =>
        $$"""
        public int Filter{{index}}(bool flag)
        {
            try
            {
                return {{index}};
            }
            catch (System.Exception ex)
            {
                if (ShouldKeep())
                {
                    return ex.Message.Length;
                }
                else
                {
                    throw;
                }
            }
        }
        """;

    /// <summary>Builds a catch that already carries a when filter, which is clean.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateWhenFilteredCatch(int index) =>
        $$"""
        public int Guard{{index}}(bool flag)
        {
            try
            {
                return {{index}};
            }
            catch (System.Exception ex) when (flag)
            {
                return ex.Message.Length;
            }
        }
        """;
}
