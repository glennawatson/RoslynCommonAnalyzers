// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for disposal-pattern analyzer benchmarks.</summary>
internal static class DisposePatternBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating disposal patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit disposal-pattern rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating disposal type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type that walks every exit the no-diagnostic path takes.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers the rejections in the order the analyzer takes them: a type that signs no contract at all, a
    /// sealed type whose plain <c>Dispose()</c> is already complete, a derived type that inherits the
    /// pattern, and the full pattern itself — which is the only shape that pays for the body scan.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class Plain{{index}}
           {
               public int Value { get; set; }
           }

           public sealed class Handle{{index}} : IDisposable
           {
               private bool _closed;

               public void Dispose() => _closed = true;
           }

           public class Connection{{index}} : IDisposable
           {
               private bool _disposed;

               ~Connection{{index}}() => Dispose(false);

               public void Dispose()
               {
                   Dispose(true);
                   GC.SuppressFinalize(this);
               }

               protected virtual void Dispose(bool disposing) => _disposed = disposing;
           }

           public class PooledConnection{{index}} : Connection{{index}}
           {
               protected override void Dispose(bool disposing) => base.Dispose(disposing);
           }
           """;

    /// <summary>Builds the types that reach a report.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// The finalizable <c>Leaky</c> type chains correctly but never takes the instance off the finalizer
    /// queue — the repairable clause the code-fix benchmark applies its fix to. The derived <c>Hidden</c>
    /// type re-declares a parameterless <c>Dispose()</c> that hides the base's, exercising the derived-type
    /// report, which has no fix.
    /// </remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class Leaky{{index}} : IDisposable
           {
               private bool _disposed;

               ~Leaky{{index}}() => Dispose(false);

               public void Dispose()
               {
                   Dispose(true);
               }

               protected virtual void Dispose(bool disposing) => _disposed = disposing;
           }

           public class Hidden{{index}} : Leaky{{index}}
           {
               public new void Dispose()
               {
               }
           }
           """;
}
