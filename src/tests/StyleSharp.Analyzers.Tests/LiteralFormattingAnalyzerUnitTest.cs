// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLiteral = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.LiteralFormattingAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the opt-in literal-formatting rules SST1191 and SST1192.</summary>
public class LiteralFormattingAnalyzerUnitTest
{
    /// <summary>Verifies a long separator-free base-10 integer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LongIntegerWithoutSeparatorsReportedAsync()
        => await VerifyLiteral.VerifyAnalyzerAsync(
            """
            public class C
            {
                private const int Big = {|SST1191:1000000|};
                private const long Tagged = {|SST1191:1000000L|};
                private const int Small = 100;
                private const int Grouped = 1_000_000;
                private const int Hex = 0xFFFFFF;
            }
            """);

    /// <summary>Verifies a string literal embedding a raw tab is reported while an escaped tab is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RawControlCharacterReportedAsync()
        => await VerifyLiteral.VerifyAnalyzerAsync(
            "public class C\n{\n    private const string Raw = {|SST1192:\"a\tb\"|};\n    private const string Escaped = \"a\\tb\";\n    private const string Plain = \"hello\";\n}\n");
}
