// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeNonce = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1001ConstantAeadNonceAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1001 (an AEAD encrypt call must not use a constant or reused nonce).</summary>
public class ConstantAeadNonceAnalyzerUnitTest
{
    /// <summary>Verifies an inline all-zero <c>new byte[N]</c> nonce to AesGcm.Encrypt is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InlineZeroNonceToAesGcmReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public void M(byte[] key, byte[] plaintext, byte[] ciphertext, byte[] tag)
                {
                    using var aes = new AesGcm(key, 16);
                    aes.Encrypt({|SES1001:new byte[12]|}, plaintext, ciphertext, tag);
                }
            }
            """);

    /// <summary>Verifies a literal constant byte array nonce to AesCcm.Encrypt is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralNonceToAesCcmReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public void M(byte[] key, byte[] plaintext, byte[] ciphertext, byte[] tag)
                {
                    using var aes = new AesCcm(key);
                    aes.Encrypt({|SES1001:new byte[] { 1, 2, 3, 4, 5, 6, 7 }|}, plaintext, ciphertext, tag);
                }
            }
            """);

    /// <summary>Verifies a shared <c>static readonly</c> field nonce to ChaCha20Poly1305.Encrypt is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticReadonlyFieldNonceToChaChaReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                private static readonly byte[] Nonce = new byte[12];

                public void M(byte[] key, byte[] plaintext, byte[] ciphertext, byte[] tag)
                {
                    using var cipher = new ChaCha20Poly1305(key);
                    cipher.Encrypt({|SES1001:Nonce|}, plaintext, ciphertext, tag);
                }
            }
            """);

    /// <summary>Verifies the nonce passed by name is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedNonceArgumentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public void M(byte[] key, byte[] plaintext, byte[] ciphertext, byte[] tag)
                {
                    using var aes = new AesGcm(key, 16);
                    aes.Encrypt(plaintext: plaintext, nonce: {|SES1001:new byte[12]|}, ciphertext: ciphertext, tag: tag);
                }
            }
            """);

    /// <summary>Verifies a fresh random nonce held in a local is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FreshRandomNonceIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public void M(byte[] key, byte[] plaintext, byte[] ciphertext, byte[] tag)
                {
                    using var aes = new AesGcm(key, 16);
                    byte[] nonce = RandomNumberGenerator.GetBytes(12);
                    aes.Encrypt(nonce, plaintext, ciphertext, tag);
                }
            }
            """);

    /// <summary>Verifies a nonce produced by a call per encryption is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InlineRandomNonceIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public void M(byte[] key, byte[] plaintext, byte[] ciphertext, byte[] tag)
                {
                    using var aes = new AesGcm(key, 16);
                    aes.Encrypt(RandomNumberGenerator.GetBytes(12), plaintext, ciphertext, tag);
                }
            }
            """);

    /// <summary>Verifies a per-instance readonly field nonce is not reported (only shared static fields are).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceReadonlyFieldNonceIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                private readonly byte[] _nonce;

                public C() => _nonce = RandomNumberGenerator.GetBytes(12);

                public void M(byte[] key, byte[] plaintext, byte[] ciphertext, byte[] tag)
                {
                    using var aes = new AesGcm(key, 16);
                    aes.Encrypt(_nonce, plaintext, ciphertext, tag);
                }
            }
            """);

    /// <summary>Verifies an inline array with a non-constant element is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantArrayNonceIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public void M(byte[] key, byte b, byte[] plaintext, byte[] ciphertext, byte[] tag)
                {
                    using var aes = new AesGcm(key, 16);
                    aes.Encrypt(new byte[] { b, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, plaintext, ciphertext, tag);
                }
            }
            """);

    /// <summary>Verifies a constant nonce passed to an unrelated Encrypt method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonAeadEncryptIsCleanAsync()
        => await VerifyNet90Async(
            """
            public sealed class FakeCipher
            {
                public void Encrypt()
                {
                }

                public void Encrypt(byte[] data)
                {
                }
            }

            public class C
            {
                public void M()
                {
                    var cipher = new FakeCipher();
                    cipher.Encrypt();
                    cipher.Encrypt(new byte[12]);
                }
            }
            """);

    /// <summary>Verifies a named non-nonce first argument on an unrelated Encrypt method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedNonNonceArgumentIsCleanAsync()
        => await VerifyNet90Async(
            """
            public sealed class FakeCipher
            {
                public void Encrypt(byte[] data)
                {
                }
            }

            public class C
            {
                public void M()
                {
                    var cipher = new FakeCipher();
                    cipher.Encrypt(data: new byte[12]);
                }
            }
            """);

    /// <summary>Verifies the rule stays silent on a framework without the AEAD types (net472).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenAeadUnavailableAsync()
    {
        const string Source = """
                              public sealed class AesGcm
                              {
                                  public AesGcm(byte[] key, int tagSizeInBytes)
                                  {
                                  }

                                  public void Encrypt(byte[] nonce, byte[] plaintext, byte[] ciphertext, byte[] tag)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(byte[] key, byte[] plaintext, byte[] ciphertext, byte[] tag)
                                  {
                                      var aes = new AesGcm(key, 16);
                                      aes.Encrypt(new byte[12], plaintext, ciphertext, tag);
                                  }
                              }
                              """;

        var test = new AnalyzeNonce.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where the AEAD types exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeNonce.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
