// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.FieldNamingAnalyzer,
    StyleSharp.Analyzers.NamingRenameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the field naming rules (SST1303/SST1304/SST1307/SST1309/SST1311).</summary>
public class FieldNamingAnalyzerUnitTest
{
    /// <summary>Verifies conventional fields produce no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ValidAsync() => Verify.VerifyAnalyzerAsync(
        "public class C { public const int Max = 1; private int _value; public int Count; }");

    /// <summary>Verifies a camelCase const is reported (SST1303) and renamed to PascalCase.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstAsync() =>
        Verify.VerifyCodeFixAsync(
            "public class C { public const int {|SST1303:max|} = 1; }",
            "public class C { public const int Max = 1; }");

    /// <summary>Verifies a camelCase static readonly field is reported (SST1311) and renamed to PascalCase.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticReadonlyAsync() =>
        Verify.VerifyCodeFixAsync(
            "public class C { private static readonly int {|SST1311:max|} = 1; }",
            "public class C { private static readonly int Max = 1; }");

    /// <summary>Verifies a camelCase non-private readonly field is reported (SST1304) and renamed to PascalCase.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonPrivateReadonlyAsync() =>
        Verify.VerifyCodeFixAsync(
            "public class C { public readonly int {|SST1304:value|}; }",
            "public class C { public readonly int Value; }");

    /// <summary>Verifies a camelCase accessible field is reported (SST1307) and renamed to PascalCase.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AccessibleAsync() =>
        Verify.VerifyCodeFixAsync(
            "public class C { internal int {|SST1307:value|}; }",
            "public class C { internal int Value; }");

    /// <summary>Verifies a PascalCase private field is reported (SST1309) and renamed to _camelCase.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateAsync() =>
        Verify.VerifyCodeFixAsync(
            "public class C { private int {|SST1309:Value|}; }",
            "public class C { private int _value; }");
}
