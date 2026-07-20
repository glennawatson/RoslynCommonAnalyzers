// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = SecuritySharp.Analyzers.Tests.CSharpCodeFixVerifier<
    SecuritySharp.Analyzers.Ses1005NonConstantTimeSecretComparisonAnalyzer,
    SecuritySharp.Analyzers.Ses1005NonConstantTimeSecretComparisonCodeFixProvider>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1005 (a secret must be compared in constant time).</summary>
public class NonConstantTimeSecretComparisonAnalyzerUnitTest
{
    /// <summary>Verifies a byte-buffer <c>SequenceEqual</c> of secrets is reported and rewritten to <c>FixedTimeEquals</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ByteSequenceEqualReportedAndFixedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public bool Verify(byte[] mac, byte[] expectedMac) => {|SES1005:mac.SequenceEqual(expectedMac)|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool Verify(byte[] mac, byte[] expectedMac) => global::System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(mac, expectedMac);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the static <c>Enumerable.SequenceEqual(a, b)</c> form is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticSequenceEqualReportedAndFixedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public bool Check(byte[] hmac, byte[] tag) => {|SES1005:Enumerable.SequenceEqual(hmac, tag)|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool Check(byte[] hmac, byte[] tag) => global::System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(hmac, tag);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a byte-span <c>SequenceEqual</c> is reported and fixed with the short name when the namespace is imported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ByteSpanSequenceEqualReportedAndFixedWithImportAsync()
    {
        const string Source = """
                              using System;
                              using System.Security.Cryptography;

                              public class C
                              {
                                  public bool Verify(ReadOnlySpan<byte> signature, ReadOnlySpan<byte> expected)
                                      => {|SES1005:signature.SequenceEqual(expected)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Security.Cryptography;

                                   public class C
                                   {
                                       public bool Verify(ReadOnlySpan<byte> signature, ReadOnlySpan<byte> expected)
                                           => CryptographicOperations.FixedTimeEquals(signature, expected);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a secret operand reached through a method call is named from the call and reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvocationNamedOperandReportedAndFixedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public bool Verify(byte[] provided, byte[] key) => {|SES1005:provided.SequenceEqual(ComputeHmac(key))|};

                                  private static byte[] ComputeHmac(byte[] key) => key;
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool Verify(byte[] provided, byte[] key) => global::System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(provided, ComputeHmac(key));

                                       private static byte[] ComputeHmac(byte[] key) => key;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a secret comparison inside a verify-shaped local function is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecretComparisonInVerifyLocalFunctionReportedAndFixedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public bool Run(byte[] expected, byte[] actual)
                                  {
                                      bool Verify() => {|SES1005:expected.SequenceEqual(actual)|};
                                      return Verify();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool Run(byte[] expected, byte[] actual)
                                       {
                                           bool Verify() => global::System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, actual);
                                           return Verify();
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies <c>expected</c>/<c>actual</c> operands are reported inside a verify-shaped method.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpectationOperandsInVerifyMethodReportedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public bool VerifyDigest(byte[] expected, byte[] actual) => {|SES1005:expected.SequenceEqual(actual)|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool VerifyDigest(byte[] expected, byte[] actual) => global::System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, actual);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a secret member-access operand (<c>request.Signature</c>) compared with <c>==</c> is reported (no fix).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberAccessSecretOperandReportedAsync()
        => await VerifyReportedNoFixAsync(
            """
            public class Request
            {
                public string Signature { get; set; }
            }

            public class C
            {
                public bool Check(Request request, string provided) => {|SES1005:request.Signature == provided|};
            }
            """);

    /// <summary>Verifies a string <c>==</c> comparison of a token is reported (no fix; string has no byte fix).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringTokenEqualityReportedAsync()
        => await VerifyReportedNoFixAsync(
            """
            public class C
            {
                public bool Check(string token, string expectedToken) => {|SES1005:token == expectedToken|};
            }
            """);

    /// <summary>Verifies a string <c>!=</c> comparison of a secret is reported (no fix).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringSecretInequalityReportedAsync()
        => await VerifyReportedNoFixAsync(
            """
            public class C
            {
                public bool Differ(string secret, string provided) => {|SES1005:secret != provided|};
            }
            """);

    /// <summary>Verifies an instance <c>string.Equals</c> comparison of a secret is reported (no fix).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringEqualsReportedAsync()
        => await VerifyReportedNoFixAsync(
            """
            using System;

            public class C
            {
                public bool Check(string token, string provided) => {|SES1005:token.Equals(provided, StringComparison.Ordinal)|};
            }
            """);

    /// <summary>Verifies the static <c>object.Equals(a, b)</c> of secret buffers is reported (no fix; not SequenceEqual).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectEqualsReportedAsync()
        => await VerifyReportedNoFixAsync(
            """
            public class C
            {
                public bool Check(byte[] mac, byte[] expectedMac) => {|SES1005:object.Equals(mac, expectedMac)|};
            }
            """);

    /// <summary>Verifies a string <c>SequenceEqual</c> of a secret is reported but not fixed (string is not a byte buffer).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringSequenceEqualReportedNotFixedAsync()
        => await VerifyReportedNoFixAsync(
            """
            using System.Linq;

            public class C
            {
                public bool Check(string token, string expectedToken) => {|SES1005:token.SequenceEqual(expectedToken)|};
            }
            """);

    /// <summary>Verifies a <c>SequenceEqual</c> with an equality comparer is reported but not fixed (no constant-time twin).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparerSequenceEqualReportedNotFixedAsync()
        => await VerifyReportedNoFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public bool Check(byte[] mac, byte[] expectedMac, IEqualityComparer<byte> comparer)
                    => {|SES1005:mac.SequenceEqual(expectedMac, comparer)|};
            }
            """);

    /// <summary>Verifies <c>expected</c>/<c>actual</c> operands are ignored outside a verify-shaped method.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpectationOperandsOutsideVerifyMethodCleanAsync()
        => await VerifyAsync(
            """
            using System.Linq;

            public class C
            {
                public bool Render(byte[] expected, byte[] actual) => expected.SequenceEqual(actual);
            }
            """);

    /// <summary>Verifies <c>expected</c>/<c>actual</c> operands in a property body (no enclosing method) are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpectationOperandsInPropertyBodyCleanAsync()
        => await VerifyAsync(
            """
            using System.Linq;

            public class C
            {
                public byte[] Expected { get; set; }

                public byte[] Actual { get; set; }

                public bool AreEqual => Expected.SequenceEqual(Actual);
            }
            """);

    /// <summary>Verifies a non-secret byte-buffer comparison is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonSecretByteComparisonCleanAsync()
        => await VerifyAsync(
            """
            using System.Linq;

            public class C
            {
                public bool Check(byte[] data, byte[] other) => data.SequenceEqual(other);
            }
            """);

    /// <summary>Verifies a secret comparison against a constant (emptiness check) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecretComparedToConstantCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public bool IsEmpty(string token) => token == "";
            }
            """);

    /// <summary>Verifies a secret compared to <c>null</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecretComparedToNullCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public bool IsAbsent(byte[] mac) => mac == null;
            }
            """);

    /// <summary>Verifies a secret-named value of an unsupported type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecretNamedNonBufferTypeCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public bool Check(int tag, int other) => tag.Equals(other);
            }
            """);

    /// <summary>Verifies a secret-named receiver type with non-secret operands is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecretNamedReceiverTypeCleanAsync()
        => await VerifyAsync(
            """
            public static class TokenStore
            {
                public static bool Match(byte[] left, byte[] right) => TokenStore.Equals(left, right);
            }
            """);

    /// <summary>Verifies a custom single-argument static method named like a comparison is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomSingleArgumentStaticSequenceEqualCleanAsync()
        => await VerifyAsync(
            """
            public static class Marker
            {
                public static bool SequenceEqual(byte[] value) => value.Length == 0;
            }

            public class C
            {
                public bool Check(byte[] mac) => Marker.SequenceEqual(mac);
            }
            """);

    /// <summary>Verifies a static <c>SequenceEqual</c> passed by name is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedArgumentStaticSequenceEqualCleanAsync()
        => await VerifyAsync(
            """
            using System.Linq;

            public class C
            {
                public bool Check(byte[] mac, byte[] expectedMac) => Enumerable.SequenceEqual(first: mac, second: expectedMac);
            }
            """);

    /// <summary>Verifies a custom non-bool <c>SequenceEqual</c> on a secret is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomNonBoolSequenceEqualCleanAsync()
        => await VerifyAsync(
            """
            public class Buffer
            {
                public void SequenceEqual(Buffer other)
                {
                }
            }

            public class C
            {
                public void Check(Buffer mac, Buffer other) => mac.SequenceEqual(other);
            }
            """);

    /// <summary>Verifies the rule reports but declines to fix when <c>FixedTimeEquals</c> is absent from the resolved type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReportedButUnfixedWhenFixedTimeEqualsAbsentAsync()
    {
        const string Source = """
                              using System.Linq;

                              namespace System.Security.Cryptography
                              {
                                  public static class CryptographicOperations
                                  {
                                  }
                              }

                              public class C
                              {
                                  public bool Verify(byte[] mac, byte[] expectedMac) => {|SES1005:mac.SequenceEqual(expectedMac)|};
                              }
                              """;

        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
            TestCode = Source,
            FixedCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule stays silent on a framework without <c>CryptographicOperations</c> (net472).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenCryptographicOperationsUnavailableAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public bool Verify(byte[] mac, byte[] expectedMac) => mac.SequenceEqual(expectedMac);
                              }
                              """;

        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-and-fix verification against the .NET 9 reference assemblies (where the crypto type exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification for a reported comparison the code fix intentionally leaves unchanged.</summary>
    /// <param name="source">The source with diagnostic markup; also the expected (unchanged) fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyReportedNoFixAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
