// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using AnalyzeChain = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1104WeakenedCertificateChainValidationAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1104 (certificate-chain validation must not be deliberately weakened).</summary>
public class WeakenedCertificateChainValidationAnalyzerUnitTest
{
    /// <summary>Verifies parentheses and either side of nested flag combinations retain weakening fields.</summary>
    /// <param name="value">The flag expression that suppresses chain errors.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("((X509VerificationFlags.AllFlags))")]
    [Arguments("X509VerificationFlags.AllFlags | X509VerificationFlags.NoFlag")]
    [Arguments("(X509VerificationFlags.NoFlag | X509VerificationFlags.AllowUnknownCertificateAuthority)")]
    [Arguments("(X509VerificationFlags.NoFlag | X509VerificationFlags.IgnoreNotTimeValid) | (X509VerificationFlags.AllFlags)")]
    [Arguments("(X509VerificationFlags)0 | X509VerificationFlags.AllFlags")]
    public Task NestedWeakeningFlagsAreReportedAsync(string value) =>
        VerifyNet90Async($$"""
        using System.Security.Cryptography.X509Certificates;
        class C { void M(X509ChainPolicy policy) { policy.VerificationFlags = {|SES1104:{{value}}|}; } }
        """);

    /// <summary>Verifies values that do not bind directly to weakening fields remain silent.</summary>
    /// <param name="member">The policy member being assigned.</param>
    /// <param name="value">The value whose symbol is not a weakening field.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("RevocationMode", "(X509RevocationMode)0")]
    [Arguments("RevocationMode", "default(X509RevocationMode)")]
    [Arguments("RevocationMode", "X509RevocationMode.Online | X509RevocationMode.Offline")]
    [Arguments("VerificationFlags", "(X509VerificationFlags)0")]
    [Arguments("VerificationFlags", "(X509VerificationFlags)0 | X509VerificationFlags.NoFlag")]
    [Arguments("VerificationFlags", "X509VerificationFlags.AllFlags & X509VerificationFlags.NoFlag")]
    [Arguments("VerificationFlags", "((X509VerificationFlags.NoFlag))")]
    public Task NonFieldAndSafeValuesAreCleanAsync(string member, string value) =>
        VerifyNet90Async($$"""
        using System.Security.Cryptography.X509Certificates;
        class C { void M(X509ChainPolicy policy) { policy.{{member}} = {{value}}; } }
        """);

    /// <summary>Verifies parentheses do not hide the NoCheck revocation field.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ParenthesizedNoCheckIsReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Security.Cryptography.X509Certificates;
            class C
            {
                void M(X509ChainPolicy policy)
                {
                    policy.RevocationMode = {|SES1104:((X509RevocationMode.NoCheck))|};
                }
            }
            """);

    /// <summary>Verifies unrelated assignments and same-named fields are rejected before policy analysis.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonPolicyAssignmentTargetsAreCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Security.Cryptography.X509Certificates;
            class C
            {
                X509RevocationMode RevocationMode;
                void M(X509RevocationMode[] values, X509ChainPolicy policy)
                {
                    values[0] = X509RevocationMode.NoCheck;
                    RevocationMode = X509RevocationMode.NoCheck;
                    policy.UrlRetrievalTimeout = default;
                    int value = 0;
                    value = 1;
                }
            }
            """);

    /// <summary>Verifies unresolved members and fields belonging to another enum are ignored.</summary>
    /// <param name="assignment">The invalid assignment under analysis.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("missing.RevocationMode = X509RevocationMode.NoCheck;")]
    [Arguments("policy.RevocationMode = Other.NoCheck;")]
    [Arguments("policy.VerificationFlags = Other.AllFlags;")]
    public Task UnresolvedOrMismatchedSymbolsAreCleanAsync(string assignment) =>
        new AnalyzeChain.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = $$"""
                       using System.Security.Cryptography.X509Certificates;
                       enum Other { NoCheck, AllFlags }
                       class C { void M(X509ChainPolicy policy) { {{assignment}} } }
                       """,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies setting <c>RevocationMode</c> to <c>NoCheck</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RevocationModeNoCheckReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task VerificationFlagsAllowUnknownCaReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task VerificationFlagsAllFlagsReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task VerificationFlagsOrCombinationReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ObjectInitializerWeakeningReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RevocationModeOnlineIsCleanAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task VerificationFlagsNoFlagIsCleanAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task VerificationFlagsNarrowRevocationFlagIsCleanAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameNamedMemberOnUnrelatedTypeIsCleanAsync() =>
        VerifyNet90Async(
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

        var test = new AnalyzeChain.Test { ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard10, TestCode = Source };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where the chain types exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeChain.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source };

        await test.RunAsync(CancellationToken.None);
    }
}
