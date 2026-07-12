// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for virtual-call-in-constructor analyzer benchmarks.</summary>
internal static class VirtualCallInConstructorBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating constructors.</summary>
    /// <param name="types">The number of synthetic type pairs to emit.</param>
    /// <param name="violating">Whether to emit virtual-call-in-constructor rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating base/derived pair.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating pair.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one pair whose constructors can never reach a derived override.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a sealed type (rejected on syntax alone), a
    /// private static helper, a <c>base.</c> call, a <c>sealed override</c>, a call on another object, an
    /// object initializer naming another type's virtual property, and a lambda that runs after construction.
    /// The open type is the one that matters — it is the shape that forces the walk and the binds.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public class CleanBase{{index}}
           {
               public virtual int Width { get; set; }

               public virtual void Render()
               {
               }
           }

           public class Clean{{index}} : CleanBase{{index}}
           {
               private readonly int _size;

               private readonly int _state;

               private readonly System.Action _deferred;

               public Clean{{index}}(int size)
               {
                   _size = size;
                   _state = Compute(size);
                   base.Render();
                   var view = new CleanBase{{index}} { Width = size };
                   view.Render();
                   _deferred = () => Render();
               }

               public int Size => _size + _state;

               public sealed override void Render()
               {
               }

               public void Run() => _deferred();

               private static int Compute(int size) => size * 2;
           }

           public sealed class CleanSealed{{index}} : CleanBase{{index}}
           {
               private readonly int _size;

               public CleanSealed{{index}}(int size)
               {
                   _size = size;
                   Render();
                   Width = size;
               }

               public int Size => _size;
           }
           """;

    /// <summary>Builds one pair whose derived constructor makes four overridable calls.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class DirtyBase{{index}}
           {
               public virtual int Width { get; set; }

               public virtual event System.EventHandler? Refreshed;

               public virtual void Render()
               {
               }

               protected void Raise() => Refreshed?.Invoke(this, System.EventArgs.Empty);
           }

           public class Dirty{{index}} : DirtyBase{{index}}
           {
               private readonly int _size;

               public Dirty{{index}}(int size)
               {
                   _size = size;
                   Render();
                   Width = size;
                   Refreshed += OnRefreshed;
                   this.Render();
               }

               public int Size => _size;

               public override void Render()
               {
               }

               private void OnRefreshed(object? sender, System.EventArgs e)
               {
               }
           }
           """;
}
