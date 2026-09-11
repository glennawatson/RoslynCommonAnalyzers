// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using AnalyzeCsp = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1515PermissiveContentSecurityPolicyAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1515 (a permissive Content-Security-Policy value that disables its own protection).</summary>
public class PermissiveContentSecurityPolicyAnalyzerUnitTest
{
    /// <summary>Framework-agnostic stubs of the header-setting shapes the rule recognizes.</summary>
    private const string HeadersStub = """

                                       public sealed class Headers
                                       {
                                           public string this[string key] { get => ""; set { } }

                                           public string this[string key, int order] { get => ""; set { } }

                                           public void Add(string name, string value) { }

                                           public void Append(string name, string value) { }

                                           public void Log(int code, string value) { }

                                           public void Report(string a, string b, string c) { }
                                       }

                                       public sealed class Widget
                                       {
                                           public Widget(string policy) { }
                                       }
                                       """;

    /// <summary>Verifies a <c>Headers.Add</c> call setting the CSP header to an inline-permitting value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task HeadersAddUnsafeInlineReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(Headers headers)
                    => headers.Add("Content-Security-Policy", {|SES1515:"img-src 'self'; script-src 'unsafe-inline'"|});
            }
            """);

    /// <summary>Verifies a <c>Headers.Append</c> call setting the CSP header to an eval-permitting value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task HeadersAppendUnsafeEvalReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(Headers headers)
                    => headers.Append("Content-Security-Policy", {|SES1515:"img-src 'self'; script-src 'unsafe-eval'"|});
            }
            """);

    /// <summary>Verifies an indexer assignment setting the CSP header to an inline-permitting value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IndexerAssignmentReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(Headers headers)
                    => headers["Content-Security-Policy"] = {|SES1515:"img-src 'self'; script-src 'unsafe-inline'"|};
            }
            """);

    /// <summary>Verifies the header name is matched case-insensitively.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CaseInsensitiveHeaderNameReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(Headers headers)
                    => headers.Add("content-security-policy", {|SES1515:"img-src 'self'; script-src 'unsafe-inline'"|});
            }
            """);

    /// <summary>Verifies a self-evident policy value (begins with a directive) is reported without any header call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SelfEvidentDefaultSrcUnsafeInlineReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public string M()
                    => {|SES1515:"default-src 'unsafe-inline'"|};
            }
            """);

    /// <summary>Verifies a self-evident verbatim string policy value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SelfEvidentVerbatimStringReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public string M()
                    => {|SES1515:@"style-src 'unsafe-inline'"|};
            }
            """);

    /// <summary>Verifies a self-evident policy ending in a bare wildcard source is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SelfEvidentTrailingWildcardReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public string M()
                    => {|SES1515:"base-uri *"|};
            }
            """);

    /// <summary>Verifies a self-evident policy with a wildcard source followed by a semicolon is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SelfEvidentWildcardBeforeSemicolonReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public string M()
                    => {|SES1515:"default-src *; object-src 'self'"|};
            }
            """);

    /// <summary>Verifies a wildcard set on the CSP header via an indexer assignment is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IndexerWildcardReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(Headers headers)
                    => headers["Content-Security-Policy"] = {|SES1515:"img-src 'self'; script-src *"|};
            }
            """);

    /// <summary>Verifies a string with no CSP directive token is ignored.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonCspStringIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public string M()
                    => "just a normal message with an * in it";
            }
            """);

    /// <summary>Verifies a locked-down policy with no permissive source is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DirectiveWithoutPermissiveSourceIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(Headers headers)
                    => headers.Add("Content-Security-Policy", "default-src 'self'; object-src 'none'");
            }
            """);

    /// <summary>Verifies a wildcard subdomain host is not treated as a bare wildcard source.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WildcardSubdomainHostIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public string M()
                    => "default-src 'self' *.example.com";
            }
            """);

    /// <summary>Verifies a scheme-relative wildcard host is not treated as a bare wildcard source.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SchemeWildcardHostIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public string M()
                    => "default-src https://*";
            }
            """);

    /// <summary>Verifies a string that merely mentions a directive mid-text, off a header, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ContainsDirectiveButNotHeaderIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public string M()
                    => "note: script-src 'unsafe-inline' would be unsafe";
            }
            """);

    /// <summary>Verifies a directive-like prefix without a source boundary is not treated as self-evident.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DirectivePrefixWithoutBoundaryIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public string M()
                    => "script-src-custom 'unsafe-inline'";
            }
            """);

    /// <summary>Verifies a permissive value set on a different header is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DifferentHeaderNameIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(Headers headers)
                    => headers.Add("X-Custom", "img-src 'self'; script-src 'unsafe-inline'");
            }
            """);

    /// <summary>Verifies a permissive value set on a different header via an indexer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DifferentIndexerHeaderNameIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(Headers headers)
                    => headers["X-Custom"] = "img-src 'self'; script-src 'unsafe-inline'";
            }
            """);

    /// <summary>Verifies the literal in the name slot (not the value slot) does not report.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PolicyInNameSlotIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(Headers headers)
                    => headers.Add("img-src 'self'; script-src 'unsafe-inline'", "ignored");
            }
            """);

    /// <summary>Verifies a call with more than two arguments is not treated as a header set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThreeArgumentCallIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(Headers headers)
                    => headers.Report("Content-Security-Policy", "img-src 'self'; script-src 'unsafe-inline'", "x");
            }
            """);

    /// <summary>Verifies a non-string header-name argument is not matched.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NumericNameArgumentIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(Headers headers)
                    => headers.Log(200, "img-src 'self'; script-src 'unsafe-inline'");
            }
            """);

    /// <summary>Verifies a non-literal header-name argument is not matched.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonLiteralNameArgumentIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(Headers headers, string name)
                    => headers.Add(name, "img-src 'self'; script-src 'unsafe-inline'");
            }
            """);

    /// <summary>Verifies a multi-argument indexer key is not matched as a header name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MultiArgumentIndexerIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(Headers headers)
                    => headers["Content-Security-Policy", 0] = "img-src 'self'; script-src 'unsafe-inline'";
            }
            """);

    /// <summary>Verifies a permissive value passed to a constructor is not treated as a header set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstructorArgumentIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public Widget M()
                    => new Widget("img-src 'self'; script-src 'unsafe-inline'");
            }
            """);

    /// <summary>Verifies a permissive value assigned to a plain field is not treated as a header set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SimpleAssignmentIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                private string _policy = "";

                public void M()
                    => _policy = "img-src 'self'; script-src 'unsafe-inline'";
            }
            """);

    /// <summary>Runs an analyzer-only verification with the inline header stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeCsp.Test { TestCode = source + HeadersStub };

        await test.RunAsync(CancellationToken.None);
    }
}
