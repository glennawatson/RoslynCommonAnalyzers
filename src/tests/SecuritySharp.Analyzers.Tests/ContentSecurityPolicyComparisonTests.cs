// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using AnalyzeCsp = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1515PermissiveContentSecurityPolicyAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Tests reference policy text against bound string operators and published assertion APIs.</summary>
public sealed class ContentSecurityPolicyComparisonTests
{
    /// <summary>Assertion assemblies shared by every compilation in these tests.</summary>
    private static readonly ReferenceAssemblies AssertionReferences = AnalyzerFrameworks.Net80.AddPackages(
        [new("TUnit.Assertions", "1.68.4"), new("FluentAssertions", "6.12.2"), new("NUnit", "4.2.2")]);

    /// <summary>Verifies comparison operands remain reference text across operators and assertion chains.</summary>
    /// <param name="statement">A comparison using the actual framework APIs.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("_ = policy == \"script-src 'unsafe-inline'\";")]
    [Arguments("_ = policy != \"script-src 'unsafe-inline'\";")]
    [Arguments("_ = (\"script-src 'unsafe-inline'\") == policy;")]
    [Arguments("_ = (\"script-src 'unsafe-inline'\") != policy;")]
    [Arguments("policy.Should().BeEquivalentTo(\"script-src 'unsafe-inline'\");")]
    [Arguments("policy.Should().NotBeEquivalentTo(\"script-src 'unsafe-inline'\");")]
    [Arguments("policy.Should().ContainEquivalentOf(\"script-src 'unsafe-inline'\");")]
    [Arguments("policy.Should().NotContainEquivalentOf(\"script-src 'unsafe-inline'\");")]
    [Arguments("\"script-src 'unsafe-inline'\".Should().Be(policy);")]
    [Arguments("FluentAssertions.AssertionExtensions.Should(\"script-src 'unsafe-inline'\").Be(policy);")]
    [Arguments("NUnit.Framework.Assert.That(policy, NUnit.Framework.Contains.Substring(\"script-src 'unsafe-inline'\"));")]
    [Arguments("NUnit.Framework.Assert.That(policy, NUnit.Framework.Is.EqualTo(\"script-src 'unsafe-inline'\"));")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ReferenceOperandsAreCleanAsync(string statement) =>
        VerifyAsync($$"""
            using FluentAssertions;
            class C
            {
                public void M(string policy) { {{statement}} }
            }
            """);

    /// <summary>Verifies the published TUnit assertions and ordinal string search from the reported sample.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task TUnitPolicyAssertionsAreCleanAsync() =>
        VerifyAsync("""
            using System;
            using System.Threading.Tasks;
            using TUnit.Assertions;
            using TUnit.Assertions.Extensions;
            public static class CspAssertionSample
            {
                public static async Task CheckAsync(string policy)
                {
                    await Assert.That(policy).Contains("style-src 'self' 'unsafe-inline'");
                    await Assert.That(policy).DoesNotContain("script-src 'self' 'unsafe-inline'");
                }

                public static bool HasUnsafeInline(string policy) =>
                    policy.Contains("script-src 'self' 'unsafe-inline'", StringComparison.Ordinal);
            }
            """);

    /// <summary>Verifies comparison wrappers cannot hide policy assignments or string-producing calls.</summary>
    /// <param name="statement">A policy-producing operation with expected diagnostic markup.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("_ = (headers[\"Content-Security-Policy\"] = {|SES1515:\"script-src 'unsafe-inline'\"|}) == policy;")]
    [Arguments("_ = policy != (headers[\"Content-Security-Policy\"] = {|SES1515:\"script-src 'unsafe-inline'\"|});")]
    [Arguments("(headers[\"Content-Security-Policy\"] = {|SES1515:\"script-src 'unsafe-inline'\"|}).Should().Be(policy);")]
    [Arguments("headers[\"Content-Security-Policy\"] = {|SES1515:\"script-src 'unsafe-inline'\"|}.Substring(0);")]
    [Arguments("headers[\"Content-Security-Policy\"] = {|SES1515:\"script-src 'unsafe-inline'\"|} + policy;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PolicyOperationsAreReportedAsync(string statement) =>
        VerifyAsync($$"""
            using System.Collections.Generic;
            using FluentAssertions;
            class C
            {
                public void M(string policy, Dictionary<string, string> headers) { {{statement}} }
            }
            """);

    /// <summary>Verifies an actual assertion's argument can assign a policy that must be reported.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task TUnitHeaderAssignmentIsReportedAsync() =>
        VerifyAsync("""
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using TUnit.Assertions;
            using TUnit.Assertions.Extensions;
            class C
            {
                public async Task M(Dictionary<string, string> headers) =>
                    await Assert.That(headers["Content-Security-Policy"] = {|SES1515:"script-src 'unsafe-inline'"|}).Contains("script-src");
            }
            """);

    /// <summary>Verifies user operators and lookalike methods can consume policies with side effects.</summary>
    /// <param name="statement">A noncomparison API or user-defined equality operator.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("_ = sink == {|SES1515:\"script-src 'unsafe-inline'\"|};")]
    [Arguments("_ = sink != {|SES1515:\"script-src 'unsafe-inline'\"|};")]
    [Arguments("sink.BeEquivalentTo({|SES1515:\"script-src 'unsafe-inline'\"|});")]
    [Arguments("sink.Should({|SES1515:\"script-src 'unsafe-inline'\"|});")]
    [Arguments("sink.Substring({|SES1515:\"script-src 'unsafe-inline'\"|});")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UserDefinedConsumersAreReportedAsync(string statement) =>
        VerifyAsync($$"""
            using System.Collections.Generic;
            class C
            {
                public void M(PolicySink sink) { {{statement}} }
            }
            class PolicySink
            {
                public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();
                public static bool operator ==(PolicySink sink, string policy)
                {
                    sink.Headers["Content-Security-Policy"] = policy;
                    return true;
                }
                public static bool operator !=(PolicySink sink, string policy) => !(sink == policy);
                public override bool Equals(object value) => ReferenceEquals(this, value);
                public override int GetHashCode() => base.GetHashCode();
                public void BeEquivalentTo(string policy) => Headers["Content-Security-Policy"] = policy;
                public void Should(string policy) => Headers["Content-Security-Policy"] = policy;
                public void Substring(string policy) => Headers["Content-Security-Policy"] = policy;
            }
            """);

    /// <summary>Runs the analyzer against cached framework and assertion references.</summary>
    /// <param name="source">The source including expected diagnostic markup.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeCsp.Test { TestCode = source, ReferenceAssemblies = AssertionReferences };
        await test.RunAsync(CancellationToken.None);
    }
}
