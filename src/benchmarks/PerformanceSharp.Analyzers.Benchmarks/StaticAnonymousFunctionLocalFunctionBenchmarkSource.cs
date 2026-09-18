// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds anonymous functions that invoke local functions across enclosing scopes.</summary>
internal static class StaticAnonymousFunctionLocalFunctionBenchmarkSource
{
    /// <summary>Builds capturing, method-group, nested-function, and event-callback cases.</summary>
    /// <param name="types">The number of synthetic types.</param>
    /// <param name="violating">Whether the referenced local functions are static.</param>
    /// <returns>The generated source.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Generate(int types, bool violating) =>
        BenchmarkSourceText.JoinBlocks(types, index => GenerateType(index, violating));

    /// <summary>Builds one type with local-function calls, nested scopes, and compile-time references.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether the referenced local functions are static.</param>
    /// <returns>The generated type.</returns>
    private static string GenerateType(int index, bool violating)
    {
        var modifier = violating ? "static " : string.Empty;
        var value = violating ? "1" : "value";
        return $$"""
                 public class C{{index}}
                 {
                     public System.Func<int> Capturing(int value)
                     {
                         {{modifier}}int Local() => {{value}};
                         return () => Local();
                     }

                     public System.Func<int> NonCapturing()
                     {
                         {{modifier}}int Local() => 1;
                         return () => Local();
                     }

                     public System.Func<int> AnonymousMethod(int value)
                     {
                         {{modifier}}int Local() => {{value}};
                         return delegate { return Local(); };
                     }

                     public System.Func<int> MethodGroup(int value)
                     {
                         {{modifier}}int Local() => {{value}};
                         return () => { System.Func<int> handler = Local; return handler(); };
                     }

                     public System.Func<int> NestedFunction(int value)
                     {
                         {{modifier}}int Local() => {{value}};
                         return () => { int Nested() => Local(); return Nested(); };
                     }

                     public System.Func<int> NestedLambda(int value)
                     {
                         {{modifier}}int Local() => {{value}};
                         return () => { System.Func<int> nested = () => Local(); return nested(); };
                     }

                     public System.EventHandler EventCallback(int value)
                     {
                         {{modifier}}System.Threading.Tasks.Task<int> Local() => System.Threading.Tasks.Task.FromResult({{value}});
                         return async (sender, e) => await Local();
                     }

                     {{GenerateSiblingMethods(violating)}}
                 }
                 """;
    }

    /// <summary>Builds safe siblings beside a capturing local-function call.</summary>
    /// <param name="violating">Whether the safe lambdas need the static modifier.</param>
    /// <returns>The generated methods.</returns>
    private static string GenerateSiblingMethods(bool violating)
    {
        var modifier = violating ? string.Empty : "static ";
        return $$"""
            public System.Func<int> Sibling(int value)
            {
                int Local() => value;
                System.Func<int> sibling = () => Local();
                return {{modifier}}() => 1;
            }

            public System.Func<int> LocalFunctionName(int value)
            {
                int Local() => value;
                System.Func<int> sibling = () => Local();
                return {{modifier}}() => nameof(Local).Length;
            }
            """;
    }
}
