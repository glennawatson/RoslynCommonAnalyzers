// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.LocalVariableNamingAnalyzer,
    StyleSharp.Analyzers.NamingRenameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1312 (local variables should be camelCase).</summary>
public class LocalVariableNamingAnalyzerUnitTest
{
    /// <summary>Verifies a camelCase local produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public void M() { int count = 1; } }");

    /// <summary>Verifies a const local is ignored (PascalCase is allowed for constants).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstLocalIgnoredAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public void M() { const int Max = 1; } }");

    /// <summary>Verifies a PascalCase local is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalAsync()
        => await Verify.VerifyCodeFixAsync(
            "public class C { public void M() { int {|SST1312:Count|} = 1; } }",
            "public class C { public void M() { int count = 1; } }");

    /// <summary>Verifies a PascalCase foreach variable is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForEachAsync()
        => await Verify.VerifyCodeFixAsync(
            "public class C { public void M(int[] xs) { foreach (var {|SST1312:Item|} in xs) { } } }",
            "public class C { public void M(int[] xs) { foreach (var item in xs) { } } }");
}
