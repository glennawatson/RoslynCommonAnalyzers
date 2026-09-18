// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using AnalyzeComparison = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1005NonConstantTimeSecretComparisonAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Tests the boundary between ordinary expectation names and secret-bearing operand names.</summary>
public sealed class NonConstantTimeSecretComparisonRegressionTests
{
    /// <summary>The cached references for the assertion API from the reported example.</summary>
    private static readonly ReferenceAssemblies AssertionReferences = AnalyzerFrameworks.Net90.AddPackages([new("NUnit", "4.2.2")]);

    /// <summary>Verifies validation method names do not turn ordinary string comparisons into secret checks.</summary>
    /// <param name="methodName">The enclosing method name.</param>
    /// <param name="comparison">The ordinary comparison expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ExpectationStringsAreCleanAsync(
        [Matrix("ValidateHeaders", "VerifyDigest", "Check", "Compare", "Authenticate", "Match", "Render")] string methodName,
        [Matrix(
            "actual == expected",
            "expected != actual",
            "actual.Equals(expected)",
            "string.Equals(expected, actual)",
            "object.Equals(actual, expected)",
            "actual.Equals(expected, StringComparison.Ordinal)",
            "actual.SequenceEqual(expected)",
            "Enumerable.SequenceEqual(expected, actual)")] string comparison) =>
        VerifyAsync($$"""
            using System;
            using System.Linq;
            public class C
            {
                public bool {{methodName}}(string actual, string expected) => {{comparison}};
            }
            """);

    /// <summary>Verifies expectation names remain ordinary for arrays and mutable or read-only byte spans.</summary>
    /// <param name="methodName">The enclosing method name.</param>
    /// <param name="type">The compared buffer type.</param>
    /// <param name="comparison">The content comparison expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ExpectationBuffersAreCleanAsync(
        [Matrix("ValidateHeaders", "VerifyDigest", "Check", "Compare", "Authenticate", "Match", "Render")] string methodName,
        [Matrix("byte[]", "Span<byte>", "ReadOnlySpan<byte>")] string type,
        [Matrix("actual.SequenceEqual(expected)", "expected.SequenceEqual(actual)", "MemoryExtensions.SequenceEqual<byte>(expected, actual)")] string comparison) =>
        VerifyAsync($$"""
            using System;
            using System.Linq;
            public class C
            {
                public bool {{methodName}}({{type}} actual, {{type}} expected) => {{comparison}};
            }
            """);

    /// <summary>Verifies expectation fragments alone are insufficient regardless of case, suffix, or operand position.</summary>
    /// <param name="left">The first operand name.</param>
    /// <param name="right">The second operand name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("EXPECTED", "ACTUAL")]
    [Arguments("expectedHeaders", "actualHeaders")]
    [Arguments("expected", "provided")]
    [Arguments("provided", "actual")]
    [Arguments("unexpectedValue", "actualValue")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ExpectationNameVariantsAreCleanAsync(string left, string right) =>
        VerifyAsync($$"""
            public class C
            {
                public bool Validate(string {{left}}, string {{right}}) => {{left}} == {{right}};
            }
            """);

    /// <summary>Verifies local functions, lambdas, members, and called methods do not strengthen expectation names.</summary>
    /// <param name="members">The members containing an ordinary comparison.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public bool Run(string actual, string expected) { bool Validate() => actual == expected; return Validate(); }")]
    [Arguments("public bool Validate(string actual, string expected) { Func<bool> compare = () => actual.Equals(expected); return compare(); }")]
    [Arguments("public string Expected { get; set; } public string Actual { get; set; } public bool Validate() => Expected == Actual;")]
    [Arguments("public string GetExpected() => string.Empty; public bool Validate(string actual) => actual.Equals(GetExpected());")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NestedAndMemberExpectationNamesAreCleanAsync(string members) =>
        VerifyAsync($$"""
            using System;
            public class C
            {
                {{members}}
            }
            """);

    /// <summary>Verifies explicit secret fragments remain reportable on either side of an ordinary expectation name.</summary>
    /// <param name="secretName">The secret-bearing operand name.</param>
    /// <param name="secretOnLeft">Whether the secret is the first operand.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    public async Task SecretNamesAreReportedAsync(
        [Matrix("hmac", "signature", "sig", "mac", "tag", "token", "hash", "digest", "secret", "EXPECTEDTOKEN", "actualDigest")] string secretName,
        [Matrix(false, true)] bool secretOnLeft)
    {
        var comparison = secretOnLeft ? $"{secretName} == expected" : $"expected == {secretName}";
        await VerifyAsync($$"""
            public class C
            {
                public bool Render(string {{secretName}}, string expected) => {|SES1005:{{comparison}}|};
            }
            """);
    }

    /// <summary>Either constant operand excludes the comparison while two runtime operands remain reportable.</summary>
    /// <param name="left">The left operand expression.</param>
    /// <param name="right">The right operand expression.</param>
    /// <param name="comparisonKind">The equality form used by the comparison.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    public async Task SecretComparisonRespectsConstantOperandsAsync(
        [Matrix("token", "ConstantToken")] string left,
        [Matrix("providedToken", "OtherConstantToken")] string right,
        [Matrix("==", "!=", "Equals")] string comparisonKind,
        CancellationToken cancellationToken)
    {
        var comparison = comparisonKind == "Equals"
            ? $"string.Equals({left}, {right})"
            : $"{left} {comparisonKind} {right}";
        if (left == "token" && right == "providedToken")
        {
            comparison = $"{{|SES1005:{comparison}|}}";
        }

        var test = new AnalyzeComparison.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = $$"""
                public class C
                {
                    private const string ConstantToken = "fixed";
                    private const string OtherConstantToken = "other";

                    public bool Compare(string token, string providedToken) => {{comparison}};
                }
                """,
        };
        await test.RunAsync(cancellationToken);
    }

    /// <summary>Verifies the exact NUnit assertion shape does not produce a secret comparison diagnostic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NUnitHeaderAssertionIsCleanAsync()
    {
        const string Source = """
            using NUnit.Framework;
            public class C
            {
                private static void ValidateHeaders(string[] actualHeaders, string[] expectedHeaders)
                {
                    for (var i = 0; i < actualHeaders.Length; i++)
                    {
                        var actual = actualHeaders[i];
                        var expected = expectedHeaders[i];
                        Assert.That(actual, Is.EqualTo(expected));
                    }
                }
            }
            """;
        var test = new AnalyzeComparison.Test { TestCode = Source, ReferenceAssemblies = AssertionReferences };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against shared framework references.</summary>
    /// <param name="source">The source with expected diagnostic markup.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeComparison.Test { TestCode = source, ReferenceAssemblies = AnalyzerFrameworks.Net90 };
        await test.RunAsync(CancellationToken.None);
    }
}
