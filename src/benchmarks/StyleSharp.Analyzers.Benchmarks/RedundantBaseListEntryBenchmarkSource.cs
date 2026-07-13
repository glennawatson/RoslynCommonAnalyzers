// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for redundant-base-list-entry analyzer benchmarks.</summary>
internal static class RedundantBaseListEntryBenchmarkSource
{
    /// <summary>The interfaces and base classes every generated type builds its base list from.</summary>
    private const string Preamble = """
        public interface IParent
        {
            void Run();
        }

        public interface IChild : IParent
        {
        }

        public interface IAlpha
        {
        }

        public interface IBeta
        {
        }

        public class BasePlain
        {
        }

        public class BaseRunner : IParent
        {
            public void Run()
            {
            }
        }
        """;

    /// <summary>Builds a compilation unit that exercises clean or violating base lists.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit base lists that state what the list already implies.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{Preamble}}

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating pair of types.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit violating types.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds two types whose base lists imply nothing, so every entry has to be bound and cleared.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}} : BasePlain, IAlpha, IBeta
           {
           }

           public sealed class D{{index}} : IChild
           {
               public void Run()
               {
               }
           }
           """;

    /// <summary>Builds two types, each stating one thing its base list already implies.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}} : IChild, IParent
           {
               public void Run()
               {
               }
           }

           public sealed class W{{index}} : BaseRunner, IParent
           {
           }
           """;
}
