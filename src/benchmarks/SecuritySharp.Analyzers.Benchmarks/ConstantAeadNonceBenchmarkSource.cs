// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the constant-AEAD-nonce analyzer benchmarks.</summary>
internal static class ConstantAeadNonceBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating AEAD nonce usage.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit a constant (violating) nonce.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Security.Cryptography;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating AEAD-encrypting type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => $$"""
           public sealed class C{{index}}
           {
               public void Encrypt(byte[] key, byte[] plaintext, byte[] ciphertext, byte[] tag)
               {
                   using var aes = new AesGcm(key, 16);
                   aes.Encrypt({{(violating ? "new byte[12]" : "RandomNumberGenerator.GetBytes(12)")}}, plaintext, ciphertext, tag);
               }
           }
           """;
}
