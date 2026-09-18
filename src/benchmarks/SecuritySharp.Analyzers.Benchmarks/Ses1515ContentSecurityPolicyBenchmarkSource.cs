// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Builds realistic header configuration and string comparisons for SES1515.</summary>
internal static class Ses1515ContentSecurityPolicyBenchmarkSource
{
    /// <summary>The reportable header assignments in each violating application type.</summary>
    internal const int ViolationsPerType = 5;

    /// <summary>The direct header policies whose permissions must produce diagnostics.</summary>
    private const string ViolatingStatements = """
        headers["Content-Security-Policy"] = "script-src 'unsafe-inline'";
        headers["Content-Security-Policy"] = "script-src 'unsafe-eval'";
        headers["Content-Security-Policy"] = "style-src 'unsafe-inline'";
        headers["Content-Security-Policy"] = "object-src *";
        headers["Content-Security-Policy-Report-Only"] = "script-src *";
        """;

    /// <summary>Creates a policy workload against real framework strings and dictionaries.</summary>
    /// <param name="nodes">The number of application types.</param>
    /// <param name="violating">Whether each type also configures permissive headers.</param>
    /// <returns>The generated C# source.</returns>
    internal static string Generate(int nodes, bool violating) =>
        $$"""
        using System;
        using System.Collections.Generic;
        namespace Bench;

        {{BenchmarkSourceText.JoinBlocks(nodes, index => GenerateType(index, violating))}}
        """;

    /// <summary>Creates one application type with clean paths and optional unsafe header assignments.</summary>
    /// <param name="index">The unique type index.</param>
    /// <param name="violating">Whether to include the reportable policies.</param>
    /// <returns>The type declaration.</returns>
    private static string GenerateType(int index, bool violating) =>
        $$"""
        public static class Page{{index}}
        {
            public static void Configure(IDictionary<string, string> headers, string policy)
            {
                _ = "Page loaded successfully.";
                _ = "Images matching *.example.com are permitted.";
                _ = "Example: script-src 'unsafe-inline' is reference text.";
                headers["Content-Security-Policy"] = "default-src 'self'; object-src 'none'; base-uri 'self'";
                headers["Content-Security-Policy-Report-Only"] = "script-src 'self'; style-src 'self'";
                headers["Content-Security-Policy"] = "script-src 'nonce-dGVzdG5vbmNl' 'unsafe-inline'";
                headers["Content-Security-Policy"] = "script-src 'unsafe-inline' 'sha256-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA='";
                headers["Content-Security-Policy"] = "script-src 'nonce-YWJj' 'strict-dynamic' * 'unsafe-inline'";
                headers["Content-Security-Policy"] = "default-src 'unsafe-inline'; script-src 'self'; style-src 'self'";
                headers["Content-Security-Policy"] = "script-src 'self'; script-src 'unsafe-inline'; img-src *";
                headers["Content-Security-Policy"] = "script-src 'unsafe-inline'; script-src-elem 'self'; script-src-attr 'none'";
                _ = policy.Contains("script-src 'unsafe-inline'", StringComparison.Ordinal);
                _ = policy.StartsWith("style-src 'unsafe-inline'", StringComparison.Ordinal);
                _ = "script-src 'unsafe-eval'".Equals(policy, StringComparison.Ordinal);
                _ = policy == "script-src 'unsafe-inline'";
                {{(violating ? ViolatingStatements : string.Empty)}}
            }
        }
        """;
}
