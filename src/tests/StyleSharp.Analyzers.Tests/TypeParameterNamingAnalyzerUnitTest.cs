// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.TypeParameterNamingAnalyzer,
    StyleSharp.Analyzers.NamingRenameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1314 (type parameters should begin with T).</summary>
public class TypeParameterNamingAnalyzerUnitTest
{
    /// <summary>Verifies a T-prefixed type parameter produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync() => await Verify.VerifyAnalyzerAsync("public class C<TKey> { }");

    /// <summary>Verifies a type parameter without the leading T is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterAsync()
        => await Verify.VerifyCodeFixAsync("public class C<{|SST1314:Key|}> { }", "public class C<TKey> { }");

    /// <summary>Verifies a method type parameter without the leading T is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodTypeParameterAsync()
        => await Verify.VerifyCodeFixAsync(
            "public class C { public void M<{|SST1314:Key|}>(Key key) { } }",
            "public class C { public void M<TKey>(TKey key) { } }");
}
