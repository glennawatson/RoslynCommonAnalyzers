// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for optional-parameter analysis (SST2309).</summary>
internal static class OptionalParameterBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises overloads or optional parameters.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Runtime.CompilerServices;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose defaults live inside the callee.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a member with no optional parameter — which
    /// is nearly every member in a real file, and is rejected without the interface walk — the overload pair
    /// that this rule asks for, a <c>params</c> array, which has no explicit default, a caller-info parameter,
    /// which has to stay optional, and an optional parameter that no outside caller can see.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public class C{{index}}
           {
               private const int DefaultTimeout = 30;

               public C{{index}}(string name) => Name = name;

               public string Name { get; }

               public void Send(string request) => Send(request, DefaultTimeout);

               public void Send(string request, int timeout) => Console.WriteLine(request + timeout + Name);

               public void Add(params int[] values) => Console.WriteLine(values.Length + {{index}});

               public void Trace(string message, [CallerMemberName] string caller = "") => Console.WriteLine(message + caller);

               internal void Retry(int attempts = 3) => Console.WriteLine(attempts);
           }
           """;

    /// <summary>Builds one type whose defaults every caller compiles into itself.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class V{{index}}
           {
               public V{{index}}(string name, int retries = 3)
               {
                   Name = name;
                   Retries = retries;
               }

               public string Name { get; }

               public int Retries { get; }

               public void Send(string request, int timeout = 30) => Console.WriteLine(request + timeout);

               public void Log(string message, bool verbose = false, string category = "general")
                   => Console.WriteLine(message + verbose + category);
           }
           """;
}
