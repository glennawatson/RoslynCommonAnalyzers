// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeSalt = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1002ConstantKdfSaltAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1002 (a password-based key-derivation call must not use a constant or predictable salt).</summary>
public class ConstantKdfSaltAnalyzerUnitTest
{
    /// <summary>Verifies an inline all-zero <c>new byte[N]</c> salt to the Rfc2898DeriveBytes constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InlineZeroSaltToConstructorReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(string password)
                {
                    using var kdf = new Rfc2898DeriveBytes(password, {|SES1002:new byte[16]|}, 100_000, HashAlgorithmName.SHA256);
                    return kdf.GetBytes(32);
                }
            }
            """);

    /// <summary>Verifies a literal constant byte array salt to the constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralArraySaltToConstructorReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(string password)
                {
                    using var kdf = new Rfc2898DeriveBytes(password, {|SES1002:new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }|}, 100_000, HashAlgorithmName.SHA256);
                    return kdf.GetBytes(32);
                }
            }
            """);

    /// <summary>Verifies a shared <c>static readonly</c> field salt to the constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticReadonlyFieldSaltToConstructorReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                private static readonly byte[] Salt = new byte[16];

                public byte[] M(string password)
                {
                    using var kdf = new Rfc2898DeriveBytes(password, {|SES1002:Salt|}, 100_000, HashAlgorithmName.SHA256);
                    return kdf.GetBytes(32);
                }
            }
            """);

    /// <summary>Verifies an <c>Encoding.GetBytes</c> salt over a string literal to the constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EncodingGetBytesLiteralSaltToConstructorReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;
            using System.Text;

            public class C
            {
                public byte[] M(string password)
                {
                    using var kdf = new Rfc2898DeriveBytes(password, {|SES1002:Encoding.UTF8.GetBytes("static-salt")|}, 100_000, HashAlgorithmName.SHA256);
                    return kdf.GetBytes(32);
                }
            }
            """);

    /// <summary>Verifies an <c>Encoding.GetBytes</c> salt over a <c>const</c> string to the constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EncodingGetBytesConstStringSaltReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;
            using System.Text;

            public class C
            {
                private const string SaltText = "static-salt";

                public byte[] M(string password)
                {
                    using var kdf = new Rfc2898DeriveBytes(password, {|SES1002:Encoding.UTF8.GetBytes(SaltText)|}, 100_000, HashAlgorithmName.SHA256);
                    return kdf.GetBytes(32);
                }
            }
            """);

    /// <summary>Verifies an inline all-zero salt to the static <c>Pbkdf2</c> method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InlineZeroSaltToPbkdf2ReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(string password)
                    => Rfc2898DeriveBytes.Pbkdf2(password, {|SES1002:new byte[16]|}, 100_000, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies a <c>static readonly</c> field salt to the static <c>Pbkdf2</c> method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticReadonlyFieldSaltToPbkdf2ReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                private static readonly byte[] Salt = { 1, 2, 3, 4, 5, 6, 7, 8 };

                public byte[] M(string password)
                    => Rfc2898DeriveBytes.Pbkdf2(password, {|SES1002:Salt|}, 100_000, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies a fully-qualified <c>new System.Security.Cryptography.Rfc2898DeriveBytes</c> with a fixed salt is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedConstructorReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public byte[] M(string password)
                {
                    using var kdf = new System.Security.Cryptography.Rfc2898DeriveBytes(password, {|SES1002:new byte[16]|}, 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256);
                    return kdf.GetBytes(32);
                }
            }
            """);

    /// <summary>Verifies a salt passed by name is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedSaltArgumentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(string password)
                    => Rfc2898DeriveBytes.Pbkdf2(password: password, salt: {|SES1002:new byte[16]|}, iterations: 100_000, hashAlgorithm: HashAlgorithmName.SHA256, outputLength: 32);
            }
            """);

    /// <summary>Verifies a fresh random salt held in a local is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FreshRandomSaltIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(string password)
                {
                    byte[] salt = RandomNumberGenerator.GetBytes(16);
                    using var kdf = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
                    return kdf.GetBytes(32);
                }
            }
            """);

    /// <summary>Verifies a salt produced inline by a random call is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InlineRandomSaltIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(string password)
                    => Rfc2898DeriveBytes.Pbkdf2(password, RandomNumberGenerator.GetBytes(16), 100_000, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies the random-salt <c>saltSize</c> constructor overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RandomSaltSizeOverloadIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(string password)
                {
                    using var kdf = new Rfc2898DeriveBytes(password, 16, 100_000, HashAlgorithmName.SHA256);
                    return kdf.GetBytes(32);
                }
            }
            """);

    /// <summary>Verifies a per-instance readonly field salt is not reported (only shared static fields are).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceReadonlyFieldSaltIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                private readonly byte[] _salt;

                public C() => _salt = RandomNumberGenerator.GetBytes(16);

                public byte[] M(string password)
                {
                    using var kdf = new Rfc2898DeriveBytes(password, _salt, 100_000, HashAlgorithmName.SHA256);
                    return kdf.GetBytes(32);
                }
            }
            """);

    /// <summary>Verifies an inline array salt with a non-constant element is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantArraySaltIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(string password, byte b)
                    => Rfc2898DeriveBytes.Pbkdf2(password, new byte[] { b, 2, 3, 4, 5, 6, 7, 8 }, 100_000, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies a salt chosen at runtime by a conditional expression is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalSaltIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(string password, byte[] a, byte[] b, bool flag)
                    => Rfc2898DeriveBytes.Pbkdf2(password, flag ? a : b, 100_000, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies an <c>Encoding.GetBytes</c> salt over a non-constant string is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EncodingGetBytesNonConstantSaltIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;
            using System.Text;

            public class C
            {
                public byte[] M(string password, string saltText)
                    => Rfc2898DeriveBytes.Pbkdf2(password, Encoding.UTF8.GetBytes(saltText), 100_000, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies a same-named type in another namespace (not the crypto type) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShadowingConstructorIsCleanAsync()
        => await VerifyNet90Async(
            """
            public sealed class Rfc2898DeriveBytes
            {
                public Rfc2898DeriveBytes(string password, byte[] salt)
                {
                }
            }

            public class C
            {
                public void M(string password)
                {
                    var kdf = new Rfc2898DeriveBytes(password, new byte[16]);
                }
            }
            """);

    /// <summary>Verifies a <c>GetBytes</c> call on an unrelated (non-Encoding) type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomGetBytesIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public sealed class Custom
            {
                public byte[] GetBytes(string value) => new byte[16];
            }

            public class C
            {
                public byte[] M(string password, Custom custom)
                    => Rfc2898DeriveBytes.Pbkdf2(password, custom.GetBytes("x"), 100_000, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies a constant array passed to an unrelated type's constructor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedConstructorIsCleanAsync()
        => await VerifyNet90Async(
            """
            public sealed class Kdf
            {
                public Kdf(string password, byte[] salt)
                {
                }
            }

            public class C
            {
                public void M(string password)
                {
                    var kdf = new Kdf(password, new byte[16]);
                }
            }
            """);

    /// <summary>Verifies constructing an unrelated predefined type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PredefinedTypeConstructionIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public string M() => new string('a', 5);
            }
            """);

    /// <summary>Verifies a constant array passed to an unrelated static method named <c>Pbkdf2</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedPbkdf2MethodIsCleanAsync()
        => await VerifyNet90Async(
            """
            public static class Kdf
            {
                public static byte[] Pbkdf2(string password, byte[] salt) => salt;
            }

            public class C
            {
                public byte[] M(string password)
                    => Kdf.Pbkdf2(password, new byte[16]);
            }
            """);

    /// <summary>Verifies a named non-salt first argument on an unrelated constructor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedNonSaltArgumentIsCleanAsync()
        => await VerifyNet90Async(
            """
            public sealed class Kdf
            {
                public Kdf(byte[] data, byte[] salt)
                {
                }
            }

            public class C
            {
                public void M()
                {
                    var kdf = new Kdf(salt: new byte[16], data: new byte[16]);
                }
            }
            """);

    /// <summary>Verifies the rule stays silent on a framework without the key-derivation type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenKdfUnavailableAsync()
    {
        // A framework without System.Security.Cryptography.Rfc2898DeriveBytes: the metadata probe
        // returns null, so nothing is registered. The global stub (whose metadata name is not the
        // fully-qualified one) lets the source compile without resolving the real type.
        const string Source = """
                              public sealed class Rfc2898DeriveBytes
                              {
                                  public Rfc2898DeriveBytes(string password, byte[] salt)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(string password)
                                  {
                                      var kdf = new Rfc2898DeriveBytes(password, new byte[16]);
                                  }
                              }
                              """;

        var test = new AnalyzeSalt.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard12,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where the KDF APIs exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeSalt.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
