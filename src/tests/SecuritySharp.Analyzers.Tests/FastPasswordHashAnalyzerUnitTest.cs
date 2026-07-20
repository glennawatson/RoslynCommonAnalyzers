// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeFastPasswordHash = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1009FastPasswordHashAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1009 (a password must use a slow, salted key-derivation function).</summary>
public class FastPasswordHashAnalyzerUnitTest
{
    /// <summary>Verifies a <c>SHA256.HashData</c> over an <c>Encoding.GetBytes(password)</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HashDataOverEncodedPasswordReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;
            using System.Text;

            public class C
            {
                public byte[] M(string password) => {|SES1009:SHA256.HashData(Encoding.UTF8.GetBytes(password))|};
            }
            """);

    /// <summary>Verifies an instance <c>ComputeHash</c> on a <c>SHA256</c> receiver over password bytes is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComputeHashOnSha256InstanceReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] passwordBytes)
                {
                    using var sha = SHA256.Create();
                    return {|SES1009:sha.ComputeHash(passwordBytes)|};
                }
            }
            """);

    /// <summary>Verifies a <c>SHA256.HashData</c> over a member named like a password (<c>user.Password</c>) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HashDataOverPasswordMemberReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class User
            {
                public byte[] Password { get; set; }
            }

            public class C
            {
                public byte[] M(User user) => {|SES1009:SHA256.HashData(user.Password)|};
            }
            """);

    /// <summary>Verifies a <c>SHA512</c> <c>ComputeHash</c> over a password-named method call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComputeHashOverPasswordReturningCallReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                private static byte[] GetPasswordBytes() => new byte[1];

                public byte[] M()
                {
                    using var sha = SHA512.Create();
                    return {|SES1009:sha.ComputeHash(GetPasswordBytes())|};
                }
            }
            """);

    /// <summary>Verifies a <c>MD5.HashData</c> over a <c>passwd</c>-named buffer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Md5HashDataOverPasswdReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] passwdBytes) => {|SES1009:MD5.HashData(passwdBytes)|};
            }
            """);

    /// <summary>Verifies a <c>SHA1.HashData</c> over a <c>pwd</c>-named buffer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Sha1HashDataOverPwdReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] pwdValue) => {|SES1009:SHA1.HashData(pwdValue)|};
            }
            """);

    /// <summary>Verifies a <c>SHA384.HashData</c> over a <c>passphrase</c>-named buffer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Sha384HashDataOverPassphraseReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] passphraseBytes) => {|SES1009:SHA384.HashData(passphraseBytes)|};
            }
            """);

    /// <summary>Verifies a <c>SHA256.HashData</c> over a <c>credential</c>-named buffer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HashDataOverCredentialReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] credentialBytes) => {|SES1009:SHA256.HashData(credentialBytes)|};
            }
            """);

    /// <summary>Verifies hashing a non-password buffer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonPasswordDataNotReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] data) => SHA256.HashData(data);
            }
            """);

    /// <summary>Verifies a non-member-access call whose name resembles a hash method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonMemberInvocationNotReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public byte[] M(byte[] password) => ComputeHash(password);

                private static byte[] ComputeHash(byte[] value) => value;
            }
            """);

    /// <summary>Verifies a non-hash member call (<c>Encoding.GetBytes</c>) over a password is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonHashMemberCallNotReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text;

            public class C
            {
                public byte[] M(string password) => Encoding.UTF8.GetBytes(password);
            }
            """);

    /// <summary>Verifies a custom instance <c>ComputeHash</c> on an unrelated type over a password is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomComputeHashOnUnrelatedTypeNotReportedAsync()
        => await VerifyNet90Async(
            """
            public sealed class Widget
            {
                public byte[] ComputeHash(byte[] value) => value;
            }

            public class C
            {
                public byte[] M(Widget widget, byte[] passwordBytes) => widget.ComputeHash(passwordBytes);
            }
            """);

    /// <summary>Verifies a custom static <c>HashData</c> on an unrelated type over a password is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomStaticHashDataOnUnrelatedTypeNotReportedAsync()
        => await VerifyNet90Async(
            """
            public static class Util
            {
                public static byte[] HashData(byte[] value) => value;
            }

            public class C
            {
                public byte[] M(byte[] passwordBytes) => Util.HashData(passwordBytes);
            }
            """);

    /// <summary>Verifies a zero-argument <c>GetBytes()</c> wrapper (nothing to inspect inside) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroArgumentGetBytesInputNotReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public sealed class Reader
            {
                public byte[] GetBytes() => new byte[1];
            }

            public class C
            {
                public byte[] M(Reader reader) => SHA256.HashData(reader.GetBytes());
            }
            """);

    /// <summary>Verifies an indexed element input (no traceable name) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexedElementInputNotReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[][] passwords) => SHA256.HashData(passwords[0]);
            }
            """);

    /// <summary>Verifies a keyed-hash <c>ComputeHash</c> (<c>HMACSHA256</c>) over a password is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComputeHashOnKeyedHashNotReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] passwordBytes)
                {
                    using var hmac = new HMACSHA256();
                    return hmac.ComputeHash(passwordBytes);
                }
            }
            """);

    /// <summary>Verifies an unresolved <c>HashData</c> call (no bound method) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnresolvedHashDataCallNotReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public byte[] M(byte[] password) => Unknown.HashData(password);
                              }
                              """;

        var test = new AnalyzeFastPasswordHash.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source,
            CompilerDiagnostics = CompilerDiagnostics.None
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule stays silent on a framework without the fast-hash types (netstandard1.2).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenFastHashTypesUnavailableAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public byte[] M(byte[] password)
                                      => System.Security.Cryptography.SHA256.HashData(password);
                              }
                              """;

        var test = new AnalyzeFastPasswordHash.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard12,
            TestCode = Source,
            CompilerDiagnostics = CompilerDiagnostics.None
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where the fast-hash types exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeFastPasswordHash.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
