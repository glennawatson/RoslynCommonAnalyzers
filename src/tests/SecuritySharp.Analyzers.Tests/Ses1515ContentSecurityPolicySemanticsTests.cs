// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using AnalyzeCsp = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1515PermissiveContentSecurityPolicyAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Checks effective CSP restrictions and the capabilities described by SES1515.</summary>
public sealed class Ses1515ContentSecurityPolicySemanticsTests
{
    /// <summary>Verifies sources are interpreted within their directive and effective fallback.</summary>
    /// <param name="policy">A policy without a reportable effective source.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("\"default-src 'self'; script-src 'self'; img-src *\"")]
    [Arguments("\"script-src 'self'; connect-src *; font-src *; report-uri https://example.com/'unsafe-inline'\"")]
    [Arguments("\"default-src 'unsafe-inline'; script-src 'self'; style-src 'self'\"")]
    [Arguments("\"default-src *; script-src 'self'; style-src 'self'; object-src 'none'\"")]
    [Arguments("\"style-src 'unsafe-eval'; object-src 'unsafe-inline'; base-uri 'unsafe-eval'\"")]
    [Arguments("\"script-src 'self'; script-src 'unsafe-inline'\"")]
    [Arguments("\"SCRIPT-SRC 'self'; script-src *\"")]
    [Arguments("\"script-src https://example.com/'unsafe-inline'\"")]
    [Arguments("\"script-src 'nonce-dGVzdG5vbmNl' 'unsafe-inline'\"")]
    [Arguments("\"script-src 'unsafe-inline' 'sha256-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA='\"")]
    [Arguments("\"script-src 'unsafe-inline' 'sha384-YWJj'\"")]
    [Arguments("\"style-src 'sha512-YWJj' 'unsafe-inline'\"")]
    [Arguments("\"script-src 'nonce-YWJj' 'strict-dynamic' * 'unsafe-inline'\"")]
    [Arguments("\"script-src 'strict-dynamic' * 'unsafe-inline'\"")]
    [Arguments("\"script-src 'wasm-unsafe-eval'\"")]
    [Arguments("\"script-src 'unsafe-inline'; script-src-elem 'self'; script-src-attr 'none'\"")]
    [Arguments("\"default-src 'unsafe-inline'; script-src-elem 'self'; script-src-attr 'none'; style-src-elem 'self'; style-src-attr 'none'\"")]
    [Arguments("\"script-src *; script-src-elem 'self'\"")]
    [Arguments("\"style-src *; style-src-elem 'self'\"")]
    [Arguments("\"script-src 'self'; script-src-elem 'unsafe-eval'\"")]
    [Arguments("\"script-src-elem 'self'; script-src-elem 'unsafe-inline'\"")]
    [Arguments("\"script-src; script-src 'unsafe-inline'\"")]
    [Arguments("\"script-src 'nonce-YWJj_-/+=' 'unsafe-inline'\"")]
    [Arguments("\"script-src 'none', script-src 'unsafe-inline'\"")]
    [Arguments("\"script-src 'unsafe-inline', script-src 'nonce-YWJj'\"")]
    [Arguments("\"default-src *; script-src 'self', default-src 'self'; script-src *\"")]
    [Arguments("\"style-src 'unsafe-inline', style-src 'self'\"")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task IneffectiveSourcesAreCleanAsync(string policy, CancellationToken cancellationToken) =>
        VerifyPolicyAsync(policy, null, false, cancellationToken);

    /// <summary>Verifies each diagnostic names the capability the effective directive actually permits.</summary>
    /// <param name="policy">The policy to analyze.</param>
    /// <param name="message">The complete expected diagnostic message.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("\"style-src 'unsafe-inline'\"", "The 'style-src' directive allows unrestricted inline styles")]
    [Arguments("\"SCRIPT-SRC 'unsafe-inline'\"", "The 'script-src' directive allows unrestricted inline scripts")]
    [Arguments("\"  ScRiPt-SrC\\t'UNSAFE-EVAL' ;\"", "The 'script-src' directive allows JavaScript evaluation from strings")]
    [Arguments("\"script-src 'nonce-YWJj' 'unsafe-inline' 'unsafe-eval'\"", "The 'script-src' directive allows JavaScript evaluation from strings")]
    [Arguments("\"script-src 'nonce-' 'unsafe-inline'\"", "The 'script-src' directive allows unrestricted inline scripts")]
    [Arguments("\"script-src 'sha256-?' 'unsafe-inline'\"", "The 'script-src' directive allows unrestricted inline scripts")]
    [Arguments("\"default-src *; script-src 'self'; style-src 'self'\"", "The 'default-src' directive allows embedded objects from arbitrary network hosts")]
    [Arguments("\"style-src *\"", "The 'style-src' directive allows styles from arbitrary network hosts")]
    [Arguments("\"base-uri *\"", "The 'base-uri' directive allows base URLs from arbitrary origins")]
    [Arguments("\"script-src-attr 'unsafe-inline'\"", "The 'script-src-attr' directive allows unrestricted inline script attributes")]
    [Arguments("\"style-src-attr 'unsafe-inline'\"", "The 'style-src-attr' directive allows unrestricted inline style attributes")]
    [Arguments("\"script-src 'unsafe-inline'; script-src-elem 'self'\"", "The 'script-src' directive allows unrestricted inline script attributes")]
    [Arguments("\"style-src 'unsafe-inline'; style-src-elem 'self'\"", "The 'style-src' directive allows unrestricted inline style attributes")]
    [Arguments("\"script-src-elem *\"", "The 'script-src-elem' directive allows scripts from arbitrary network hosts")]
    [Arguments("\"script-src 'unsafe-eval'; script-src-elem 'none'; script-src-attr 'none'\"", "The 'script-src' directive allows JavaScript evaluation from strings")]
    [Arguments("\"script-src 'strict-dynamic' *; style-src 'strict-dynamic' 'unsafe-inline'\"", "The 'style-src' directive allows unrestricted inline styles")]
    [Arguments("\"script-src 'nonce-==' 'unsafe-inline'\"", "The 'script-src' directive allows unrestricted inline scripts")]
    [Arguments("\"script-src 'nonce-Y=WJj' 'unsafe-inline'\"", "The 'script-src' directive allows unrestricted inline scripts")]
    [Arguments("\"script-src 'nonce-YWJj===' 'unsafe-inline'\"", "The 'script-src' directive allows unrestricted inline scripts")]
    [Arguments("\"script-src 'unsafe-inline', script-src 'unsafe-inline'\"", "The 'script-src' directive allows unrestricted inline scripts")]
    [Arguments("\"script-src 'unsafe-inline', style-src 'unsafe-inline'\"", "The 'script-src' directive allows unrestricted inline scripts")]
    [Arguments("\"script-src 'none'; style-src 'unsafe-inline', style-src 'unsafe-inline'\"", "The 'style-src' directive allows unrestricted inline styles")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MessagesDescribeEffectivePermissionsAsync(string policy, string message, CancellationToken cancellationToken) =>
        VerifyPolicyAsync(policy, message, false, cancellationToken);

    /// <summary>Verifies a report-only policy is described without claiming it changes enforcement.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ReportOnlyMessageDescribesHypotheticalPermissionAsync(CancellationToken cancellationToken) =>
        VerifyPolicyAsync(
            "\"script-src 'unsafe-inline'\"",
            "The 'script-src' directive would allow unrestricted inline scripts if enforced (report-only policy)",
            true,
            cancellationToken);

    /// <summary>Verifies report-only context survives parentheses and concatenation around a literal.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReportOnlyConcatenationPreservesContextAsync(CancellationToken cancellationToken)
    {
        var test = new AnalyzeCsp.Test
        {
            TestCode = """
                using System.Collections.Generic;
                class C
                {
                    void M(Dictionary<string, string> headers)
                    {
                        headers["Content-Security-Policy"] = "script-src 'none'";
                        headers["Content-Security-Policy-Report-Only"] = ({|#0:"script-src 'unsafe-inline'"|}) + "; object-src 'none'";
                    }
                }
                """,
        };
        test.ExpectedDiagnostics.Add(AnalyzeCsp.Diagnostic().WithLocation(0).WithMessage(
            "The 'script-src' directive would allow unrestricted inline scripts if enforced (report-only policy)"));
        await test.RunAsync(cancellationToken);
    }

    /// <summary>Verifies a header value and, where applicable, its exact diagnostic message.</summary>
    /// <param name="policy">The quoted C# literal containing the serialized policy.</param>
    /// <param name="message">The expected message, or null for a clean policy.</param>
    /// <param name="reportOnly">Whether the policy is sent as report-only.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    private static async Task VerifyPolicyAsync(string policy, string? message, bool reportOnly, CancellationToken cancellationToken)
    {
        var value = message is null ? policy : $"{{|#0:{policy}|}}";
        var header = reportOnly ? "Content-Security-Policy-Report-Only" : "Content-Security-Policy";
        var test = new AnalyzeCsp.Test
        {
            TestCode = $$"""
                using System.Collections.Generic;
                class C
                {
                    void M(Dictionary<string, string> headers) => headers["{{header}}"] = {{value}};
                }
                """,
        };
        if (message is not null)
        {
            test.ExpectedDiagnostics.Add(AnalyzeCsp.Diagnostic().WithLocation(0).WithMessage(message));
        }

        await test.RunAsync(cancellationToken);
    }
}
