// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ParameterNamingAnalyzer,
    StyleSharp.Analyzers.NamingRenameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1313 (parameters should be camelCase).</summary>
public class ParameterNamingAnalyzerUnitTest
{
    /// <summary>Verifies a camelCase parameter produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public void M(int value) { } }");

    /// <summary>Verifies a record's positional parameters are ignored (they surface as PascalCase properties).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordPositionalIgnoredAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public record Point(int X, int Y);
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """);

    /// <summary>Verifies a PascalCase parameter is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterAsync()
        => await Verify.VerifyCodeFixAsync(
            "public class C { public int M(int {|SST1313:Value|}) => Value; }",
            "public class C { public int M(int value) => value; }");
}
