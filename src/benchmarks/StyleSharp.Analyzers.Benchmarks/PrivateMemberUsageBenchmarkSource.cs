// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for private-member usage benchmarks.</summary>
internal static class PrivateMemberUsageBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating private-member usage.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit reportable private members.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one synthetic type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit reportable members.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating
            ? $$"""
              internal sealed class C{{index}}
              {
                  private int _dead;

                  private void Unused()
                  {
                  }

                  public void M()
                  {
                      _dead = {{index}};
                  }
              }
              """
            : $$"""
              internal sealed class C{{index}}
              {
                  private int _value;

                  private void Used()
                  {
                  }

                  public int M()
                  {
                      _value = {{index}};
                      Used();
                      return _value;
                  }
              }
              """;
}
