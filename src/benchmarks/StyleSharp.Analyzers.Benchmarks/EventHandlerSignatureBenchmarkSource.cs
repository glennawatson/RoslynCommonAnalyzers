// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for event-signature analyzer benchmarks.</summary>
internal static class EventHandlerSignatureBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating event signatures.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit event-signature rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating event declaration.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type per exit the no-diagnostic path takes.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// The framework handlers settle in two string comparisons, and the interface-dictated event is the
    /// expensive clean case: it walks the implemented interfaces before it is let go.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class ValueChangedEventArgs{{index}} : EventArgs
           {
               public int Value { get; set; }
           }

           public interface INotifier{{index}}
           {
               event EventHandler<ValueChangedEventArgs{{index}}> Changed;
           }

           public sealed class Slider{{index}} : INotifier{{index}}
           {
               public event EventHandler Closed;

               public event EventHandler<ValueChangedEventArgs{{index}}> Changed;

               public int Value { get; set; }
           }
           """;

    /// <summary>Builds one type that reaches the report.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Two diagnostics per type, one per message flavour: a delegate carrying a payload nothing generic can
    /// handle, and a right-shape delegate whose exact replacement is spelled out.
    /// </remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class MovedEventArgs{{index}} : EventArgs
           {
               public int Value { get; set; }
           }

           public delegate void ValueChanged{{index}}(int oldValue, int newValue);

           public delegate void MovedHandler{{index}}(object sender, MovedEventArgs{{index}} e);

           public sealed class Slider{{index}}
           {
               public event ValueChanged{{index}} Changed;

               public event MovedHandler{{index}} Moved;

               public int Value { get; set; }
           }
           """;
}
