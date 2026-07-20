// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeChain = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1104WeakenedCertificateChainValidationAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1104 (certificate-chain validation must not be deliberately weakened).</summary>
public class WeakenedCertificateChainValidationAnalyzerUnitTest
{
    /// <summary>Verifies setting <c>RevocationMode</c> to <c>NoCheck</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RevocationModeNoCheckReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography.X509Certificates;

            public class C
            {
                public void M(X509Chain chain)
                {
                    chain.ChainPolicy.RevocationMode = {|SES1104:X509RevocationMode.NoCheck|};
                }
            }
            """);

    /// <summary>Verifies setting <c>VerificationFlags</c> to <c>AllowUnknownCertificateAuthority</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VerificationFlagsAllowUnknownCaReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography.X509Certificates;

            public class C
            {
                public void M(X509ChainPolicy policy)
                {
                    policy.VerificationFlags = {|SES1104:X509VerificationFlags.AllowUnknownCertificateAuthority|};
                }
            }
            """);

    /// <summary>Verifies setting <c>VerificationFlags</c> to the ignore-everything <c>AllFlags</c> value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VerificationFlagsAllFlagsReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography.X509Certificates;

            public class C
            {
                public void M(X509ChainPolicy policy)
                {
                    policy.VerificationFlags = {|SES1104:X509VerificationFlags.AllFlags|};
                }
            }
            """);

    /// <summary>Verifies a suppressing flag inside an OR-combination is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VerificationFlagsOrCombinationReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography.X509Certificates;

            public class C
            {
                public void M(X509ChainPolicy policy)
                {
                    policy.VerificationFlags = {|SES1104:X509VerificationFlags.IgnoreEndRevocationUnknown | X509VerificationFlags.AllowUnknownCertificateAuthority|};
                }
            }
            """);

    /// <summary>Verifies the weakening is reported inside an object initializer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectInitializerWeakeningReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography.X509Certificates;

            public class C
            {
                public X509ChainPolicy M()
                    => new X509ChainPolicy
                    {
                        RevocationMode = {|SES1104:X509RevocationMode.NoCheck|},
                        VerificationFlags = {|SES1104:X509VerificationFlags.AllFlags|},
                    };
            }
            """);

    /// <summary>Verifies a revocation mode that keeps checking on (<c>Online</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RevocationModeOnlineIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography.X509Certificates;

            public class C
            {
                public void M(X509ChainPolicy policy)
                {
                    policy.RevocationMode = X509RevocationMode.Online;
                }
            }
            """);

    /// <summary>Verifies <c>VerificationFlags = NoFlag</c> (the strict default) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VerificationFlagsNoFlagIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography.X509Certificates;

            public class C
            {
                public void M(X509ChainPolicy policy)
                {
                    policy.VerificationFlags = X509VerificationFlags.NoFlag;
                }
            }
            """);

    /// <summary>Verifies a narrow, non-authority flag (<c>IgnoreEndRevocationUnknown</c> alone) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VerificationFlagsNarrowRevocationFlagIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography.X509Certificates;

            public class C
            {
                public void M(X509ChainPolicy policy)
                {
                    policy.VerificationFlags = X509VerificationFlags.IgnoreEndRevocationUnknown | X509VerificationFlags.IgnoreCtlSignerRevocationUnknown;
                }
            }
            """);

    /// <summary>Verifies a same-named member on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedMemberOnUnrelatedTypeIsCleanAsync()
        => await VerifyNet90Async(
            """
            public enum FakeRevocation { NoCheck, Online }

            public sealed class FakePolicy
            {
                public FakeRevocation RevocationMode { get; set; }
            }

            public class C
            {
                public void M(FakePolicy policy)
                {
                    policy.RevocationMode = FakeRevocation.NoCheck;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent on a framework without the framework chain-policy type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenChainPolicyUnavailableAsync()
    {
        // netstandard1.0 has no System.Security.Cryptography.X509Certificates.X509ChainPolicy, so the
        // gate resolves nothing and registers no action; the lookalike local types must not be flagged.
        const string Source = """
                              public enum X509RevocationMode { NoCheck, Online, Offline }

                              public sealed class X509ChainPolicy
                              {
                                  public X509RevocationMode RevocationMode { get; set; }
                              }

                              public class C
                              {
                                  public void M(X509ChainPolicy policy)
                                  {
                                      policy.RevocationMode = X509RevocationMode.NoCheck;
                                  }
                              }
                              """;

        var test = new AnalyzeChain.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard10,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where the chain types exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeChain.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
