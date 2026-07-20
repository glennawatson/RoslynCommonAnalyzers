// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeHomeRolled = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1007HomeRolledCryptographyAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1007 (a type must not implement a cryptographic primitive by hand).</summary>
public class HomeRolledCryptographyAnalyzerUnitTest
{
    /// <summary>Verifies a class deriving from the abstract <c>HashAlgorithm</c> primitive is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HashAlgorithmDerivativeReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class {|SES1007:MyHash|} : HashAlgorithm
            {
                protected override void HashCore(byte[] array, int ibStart, int cbSize)
                {
                }

                protected override byte[] HashFinal() => throw new System.NotImplementedException();

                public override void Initialize()
                {
                }
            }
            """);

    /// <summary>Verifies a class deriving from the abstract <c>KeyedHashAlgorithm</c> primitive is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeyedHashAlgorithmDerivativeReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class {|SES1007:MyKeyedHash|} : KeyedHashAlgorithm
            {
                protected override void HashCore(byte[] array, int ibStart, int cbSize)
                {
                }

                protected override byte[] HashFinal() => throw new System.NotImplementedException();

                public override void Initialize()
                {
                }
            }
            """);

    /// <summary>Verifies a class deriving from the abstract <c>HMAC</c> primitive is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HmacDerivativeReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class {|SES1007:MyMac|} : HMAC
            {
            }
            """);

    /// <summary>Verifies a class deriving from the abstract <c>SymmetricAlgorithm</c> primitive is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SymmetricAlgorithmDerivativeReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class {|SES1007:MyCipher|} : SymmetricAlgorithm
            {
                public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV) => throw new System.NotImplementedException();

                public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV) => throw new System.NotImplementedException();

                public override void GenerateIV()
                {
                }

                public override void GenerateKey()
                {
                }
            }
            """);

    /// <summary>Verifies a class deriving from the abstract <c>AsymmetricAlgorithm</c> primitive is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsymmetricAlgorithmDerivativeReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class {|SES1007:MyAsymmetric|} : AsymmetricAlgorithm
            {
            }
            """);

    /// <summary>Verifies a class deriving from the abstract <c>DeriveBytes</c> primitive is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeriveBytesDerivativeReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class {|SES1007:MyKdf|} : DeriveBytes
            {
                public override byte[] GetBytes(int cb) => throw new System.NotImplementedException();

                public override void Reset()
                {
                }
            }
            """);

    /// <summary>Verifies a class reaching a primitive base through a custom intermediate is reported, along with the intermediate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomIntermediateBaseReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public abstract class {|SES1007:MyHashBase|} : HashAlgorithm
            {
                protected override void HashCore(byte[] array, int ibStart, int cbSize)
                {
                }

                protected override byte[] HashFinal() => throw new System.NotImplementedException();

                public override void Initialize()
                {
                }
            }

            public class {|SES1007:MyHash|} : MyHashBase
            {
            }
            """);

    /// <summary>Verifies subclassing the concrete <c>HMACSHA256</c> algorithm to configure it is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcreteKeyedHashSubclassIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class MyConfiguredMac : HMACSHA256
            {
            }
            """);

    /// <summary>Verifies subclassing the concrete <c>Aes</c> algorithm to configure it is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcreteSymmetricAlgorithmSubclassIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class MyConfiguredAes : Aes
            {
                public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV) => throw new System.NotImplementedException();

                public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV) => throw new System.NotImplementedException();

                public override void GenerateIV()
                {
                }

                public override void GenerateKey()
                {
                }
            }
            """);

    /// <summary>Verifies a chain reaching a primitive only through a concrete algorithm is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IntermediateOverConcreteAlgorithmIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public abstract class MyMacBase : HMACSHA256
            {
            }

            public class MyMac : MyMacBase
            {
            }
            """);

    /// <summary>Verifies a class whose base list carries only an interface is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceOnlyBaseListIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class MyDisposable : System.IDisposable
            {
                public void Dispose()
                {
                }
            }
            """);

    /// <summary>Verifies a class deriving from an unrelated, non-cryptographic base is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedBaseClassIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class MyException : System.Exception
            {
            }
            """);

    /// <summary>Verifies a class with no base list is not reported (the syntactic prefilter rejects it).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoBaseListIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class Plain
            {
            }
            """);

    /// <summary>Verifies the rule stays silent on a framework without the cryptographic primitive bases (netstandard1.0).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenCryptographyUnavailableAsync()
    {
        // A framework without System.Security.Cryptography: the metadata probe resolves none of the
        // primitive bases, so nothing is registered. The source stub (whose metadata name is not the
        // fully-qualified one) lets the derivation compile without resolving a real primitive.
        const string Source = """
                              public abstract class HashAlgorithm
                              {
                              }

                              public class MyHash : HashAlgorithm
                              {
                              }
                              """;

        var test = new AnalyzeHomeRolled.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard10,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where the primitive bases exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeHomeRolled.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
