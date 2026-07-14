// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for undisposed-disposable analyzer benchmarks (SST2410).</summary>
internal static class DisposableNeverDisposedBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating disposables.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Collections.Generic;
           using System.IO;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose disposables are all accounted for.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route in turn: the using declaration the syntactic prepass drops before it
    /// binds, a local that is disposed, one that is returned, one handed to a constructor, one added to a
    /// collection, and a local of a type that is not disposable at all — which the interface walk must
    /// reject.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public void Write(byte[] bytes)
               {
                   using var stream = new MemoryStream();
                   stream.Write(bytes, 0, bytes.Length);
               }

               public void Disposed(byte[] bytes)
               {
                   var stream = new MemoryStream();
                   stream.Write(bytes, 0, bytes.Length);
                   stream.Dispose();
               }

               public Stream Owned()
               {
                   var stream = new MemoryStream();
                   return stream;
               }

               public string Wrapped(byte[] bytes)
               {
                   var stream = new MemoryStream(bytes);
                   using var reader = new StreamReader(stream);
                   return reader.ReadToEnd();
               }

               public void Collected(List<Stream> streams)
               {
                   var stream = new MemoryStream();
                   streams.Add(stream);
               }

               public string Plain()
               {
                   var builder = new object();
                   return builder.ToString();
               }
           }
           """;

    /// <summary>Builds one type whose disposables are created and dropped.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public void Write(byte[] bytes)
               {
                   var stream = new MemoryStream();
                   stream.Write(bytes, 0, bytes.Length);
               }

               public void Read(byte[] bytes)
               {
                   var stream = new MemoryStream(bytes);
                   stream.ReadByte();
               }
           }
           """;
}
