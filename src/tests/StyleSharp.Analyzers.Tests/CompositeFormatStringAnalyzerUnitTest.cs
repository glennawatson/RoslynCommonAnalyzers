// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyCompositeFormatString = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1454CompositeFormatStringAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1454CompositeFormatStringAnalyzer"/>.</summary>
public class CompositeFormatStringAnalyzerUnitTest
{
    /// <summary>Verifies malformed and placeholder-free format strings are left to runtime validation.</summary>
    /// <param name="format">The format contents.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("plain text")]
    [Arguments("}")]
    [Arguments("}x")]
    [Arguments("}}")]
    [Arguments("{{")]
    [Arguments("{")]
    [Arguments("{x}")]
    [Arguments("{12")]
    [Arguments("{0:{x}}")]
    [Arguments("{0,10:N2} {0}")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MalformedOrSatisfiedFormatIsSilentAsync(string format) =>
        VerifyCompositeFormatString.VerifyAnalyzerAsync($$"""class C { string M() => string.Format("{{format}}", 1); }""");

    /// <summary>Verifies named arguments and nonconstant formats follow the current positional scan.</summary>
    /// <param name="call">The invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("string.Format(format: \"{9}\")")]
    [Arguments("string.Format(format: \"{9}\", arg0: 1)")]
    [Arguments("string.Format(provider: null, format: \"{9}\", arg0: 1)")]
    [Arguments("string.Format(null, \"{9}\", 1)")]
    [Arguments("string.Format(format, 1)")]
    [Arguments("string.Concat(\"{9}\", 1)")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonPositionalOrNonconstantFormatIsSilentAsync(string call) =>
        VerifyCompositeFormatString.VerifyAnalyzerAsync($$"""class C { string M(string format) => {{call}}; }""");

    /// <summary>Verifies a positional format after a named provider is checked, including repeated indexes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NamedProviderWithPositionalFormatIsCheckedAsync() =>
        VerifyCompositeFormatString.VerifyAnalyzerAsync("class C { string M() => string.Format(provider: null, {|SST1454:\"text {12:N2} {0}\"|}, 1); }");

    /// <summary>Verifies a placeholder beyond the supplied argument count is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PlaceholderWithoutArgumentIsReportedAsync() =>
        VerifyCompositeFormatString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string value) => string.Format({|SST1454:"{1}"|}, value);
            }
            """);

    /// <summary>Verifies escaped braces and satisfied placeholders are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SatisfiedPlaceholdersAreCleanAsync() =>
        VerifyCompositeFormatString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string value) => string.Format("{{{0}}}", value);
            }
            """);
}
