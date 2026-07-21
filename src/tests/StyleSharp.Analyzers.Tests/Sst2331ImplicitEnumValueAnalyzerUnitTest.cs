// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyImplicit = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2331ImplicitEnumValueAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2331 (give enum members explicit values).</summary>
public class Sst2331ImplicitEnumValueAnalyzerUnitTest
{
    /// <summary>Verifies an enum with only implicit values is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AllImplicitEnumIsReportedAsync()
        => await VerifyImplicit.VerifyAnalyzerAsync(
            """
            public enum {|SST2331:Color|}
            {
                Red,
                Green,
                Blue,
            }
            """);

    /// <summary>Verifies an enum with one implicit member among explicit ones is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PartiallyImplicitEnumIsReportedAsync()
        => await VerifyImplicit.VerifyAnalyzerAsync(
            """
            public enum {|SST2331:Color|}
            {
                Red = 1,
                Green,
                Blue = 3,
            }
            """);

    /// <summary>Verifies an enum whose every member has an explicit value is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FullyExplicitEnumIsCleanAsync()
        => await VerifyImplicit.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red = 0,
                Green = 1,
                Blue = 2,
            }
            """);

    /// <summary>Verifies an empty enum has no implicit member and is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EmptyEnumIsCleanAsync()
        => await VerifyImplicit.VerifyAnalyzerAsync(
            """
            public enum Color
            {
            }
            """);
}
