// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFieldStyle = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.FieldNameStyleAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the opt-in StyleCop field-name style rules (SST1306/SST1308/SST1310).</summary>
public class FieldNameStyleAnalyzerUnitTest
{
    /// <summary>Verifies a field name containing an underscore is reported (SST1310) when the rule is enabled.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnderscoreFieldReportedAsync()
        => await VerifyFieldStyle.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int {|SST1310:my_field|};
            }
            """);

    /// <summary>Verifies a field name beginning with an upper-case letter is reported (SST1306).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UpperCaseFieldReportedAsync()
        => await VerifyFieldStyle.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int {|SST1306:Field|};
            }
            """);
}
