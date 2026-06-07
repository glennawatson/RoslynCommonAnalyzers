// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for SST1415 <c>nameof</c> benchmarks.</summary>
internal static class UseNameofBenchmarkSource
{
    /// <summary>Builds a compilation unit containing argument-exception object creations.</summary>
    /// <param name="nodes">The number of constructor calls to emit.</param>
    /// <param name="violating">Whether to emit constructions that should become <c>nameof</c>.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int nodes, bool violating)
    {
        var exceptionType = violating ? "ArgumentNullException" : "InvalidOperationException";
        return $$"""
               using System;
               namespace Bench;
               public static class NameofBench
               {
                   public static Exception? Create(string value, string other)
                   {
                       Exception? ex = null;
               {{BenchmarkSourceText.JoinLines(nodes, _ => $$"""        ex = new {{exceptionType}}("value");""")}}
                       return ex;
                   }
               }
               """;
    }
}
