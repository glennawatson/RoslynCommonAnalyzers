// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEmptyRecordBody = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RecordAnalyzer,
    StyleSharp.Analyzers.Sst1804EmptyPositionalRecordBodyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the empty-positional-record-body rule (SST1804) and its semicolon fix.</summary>
public class EmptyPositionalRecordBodyAnalyzerUnitTest
{
    /// <summary>The <c>init</c>-accessor polyfill positional records require on the test reference assemblies.</summary>
    private const string IsExternalInit =
        "\n\nnamespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }";

    /// <summary>Verifies a positional record with an empty body is reported and rewritten to a semicolon.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyBodyRewrittenToSemicolonAsync()
        => await VerifyEmptyRecordBody.VerifyCodeFixAsync(
            "public sealed record Point(int X, int Y) {|SST1804:{ }|}" + IsExternalInit,
            "public sealed record Point(int X, int Y);" + IsExternalInit);

    /// <summary>Verifies a positional record struct with an empty body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyStructBodyRewrittenToSemicolonAsync()
        => await VerifyEmptyRecordBody.VerifyCodeFixAsync(
            "public readonly record struct Size(int W, int H) {|SST1804:{ }|}" + IsExternalInit,
            "public readonly record struct Size(int W, int H);" + IsExternalInit);

    /// <summary>Verifies a semicolon-terminated positional record is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SemicolonRecordIsCleanAsync()
        => await VerifyEmptyRecordBody.VerifyAnalyzerAsync("public sealed record Point(int X, int Y);" + IsExternalInit);

    /// <summary>Verifies a positional record whose body has members is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordWithMembersIsCleanAsync()
        => await VerifyEmptyRecordBody.VerifyAnalyzerAsync(
            """
            public sealed record Point(int X, int Y)
            {
                public int Sum => X + Y;
            }
            """ + IsExternalInit);

    /// <summary>Verifies a non-positional record with an empty body is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonPositionalRecordIsCleanAsync()
        => await VerifyEmptyRecordBody.VerifyAnalyzerAsync("public sealed record Empty { }");
}
