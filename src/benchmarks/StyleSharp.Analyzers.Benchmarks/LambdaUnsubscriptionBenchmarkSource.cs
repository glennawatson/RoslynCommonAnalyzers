// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for lambda-unsubscription analyzer benchmarks (SST2449).</summary>
internal static class LambdaUnsubscriptionBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating subtractions.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           public sealed class Stage
           {
               public static Stage operator -(Stage source, Func<int> selector) => source;
           }

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose subtractions all remove something (or are not removals at all).</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a numeric <c>-=</c> and stored-delegate
    /// and method-group removals (the common cases, which must not bind), and a custom subtraction operator
    /// fed a lambda — the one shape that reaches the semantic model and is excused there.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public event EventHandler Saved;

               private EventHandler _handler;

               private int _budget;

               public C{{index}}()
               {
                   _handler = (sender, args) => { };
                   Saved += _handler;
                   Saved += OnSaved;
               }

               public Stage Stages { get; set; } = new Stage();

               public void Detach()
               {
                   Saved -= _handler;
                   Saved -= OnSaved;
                   _budget -= 1;
                   Stages -= () => 0;
               }

               public int Budget => _budget;

               private void OnSaved(object sender, EventArgs e)
               {
               }
           }
           """;

    /// <summary>Builds one type that unsubscribes an event and a delegate field with fresh lambdas.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block, containing two violations.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public event EventHandler Saved;

               private Action _pipeline = () => { };

               public V{{index}}() => Saved += (sender, args) => { };

               public void Detach()
               {
                   Saved -= (sender, args) => { };
                   _pipeline -= () => { };
               }

               public void Run() => _pipeline();
           }
           """;
}
