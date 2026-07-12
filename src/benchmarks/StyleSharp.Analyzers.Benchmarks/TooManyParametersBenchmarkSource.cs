// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for parameter-count analyzer benchmarks.</summary>
internal static class TooManyParametersBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating parameter-count patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit parameter-count rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Runtime.CompilerServices;
           using System.Runtime.InteropServices;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating parameter-count block.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating block.</param>
    /// <returns>The generated block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one block whose every signature is short enough or exempt.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated block.</returns>
    /// <remarks>
    /// Covers every route the no-diagnostic path takes: a short signature rejected on its count alone, a
    /// signature exactly at the limit, an extension receiver and a caller-info tail that the count excludes,
    /// a deconstructor, a P/Invoke, and a positional record. No clean signature reaches a semantic bind,
    /// which is what this corpus exists to prove.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public C{{index}}(int first, int second) => Total = first + second;

               public int Total { get; }

               public int Add(int first, int second) => first + second;

               public int AtLimit(int a, int b, int c, int d, int e, int f, int g) => a + b + c + d + e + f + g;

               public void Deconstruct(out int a, out int b, out int c, out int d, out int e, out int f, out int g, out int h)
               {
                   a = b = c = d = e = f = g = h = Total;
               }

               public int Log(
                   int a,
                   int b,
                   int c,
                   int d,
                   int e,
                   int f,
                   int g,
                   [CallerMemberName] string member = "",
                   [CallerFilePath] string file = "",
                   [CallerLineNumber] int line = 0)
                   => a + b + c + d + e + f + g + member.Length + file.Length + line;
           }

           public record R{{index}}(int A, int B, int C, int D, int E, int F, int G, int H, int I, int J);

           public static class E{{index}}
           {
               public static int Sum(this int[] values, int a, int b, int c, int d, int e, int f, int g)
                   => values.Length + a + b + c + d + e + f + g;
           }

           public static class N{{index}}
           {
               [DllImport("native")]
               public static extern int Send(int a, int b, int c, int d, int e, int f, int g, int h);
           }
           """;

    /// <summary>Builds one block whose over-limit signatures are all reported.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated block.</returns>
    /// <remarks>
    /// The interface and its implementation exercise the semantic route: the interface declares the shape and
    /// is reported, and the implementation is exempt only after a bind proves the interface dictates it.
    /// Four diagnostics per block: the two wide methods, the wide constructor, and the interface method.
    /// </remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public V{{index}}(int a, int b, int c, int d, int e, int f, int g, int h) => Total = a + b + c + d + e + f + g + h;

               public int Total { get; }

               public int Wide(int a, int b, int c, int d, int e, int f, int g, int h) => a + b + c + d + e + f + g + h;

               public int Wider(int a, int b, int c, int d, int e, int f, int g, int h, int i)
                   => a + b + c + d + e + f + g + h + i;
           }

           public interface IW{{index}}
           {
               int Run(int a, int b, int c, int d, int e, int f, int g, int h);
           }

           public sealed class W{{index}} : IW{{index}}
           {
               public int Run(int a, int b, int c, int d, int e, int f, int g, int h) => a + b + c + d + e + f + g + h;
           }
           """;
}
