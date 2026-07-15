// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.LiteralFormattingAnalyzer,
    StyleSharp.Analyzers.Sst1119IrregularDigitGroupingCodeFixProvider>;
using VerifyGrouping = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.LiteralFormattingAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1119 (irregular digit-separator grouping).</summary>
public class IrregularDigitGroupingAnalyzerUnitTest
{
    /// <summary>A decimal literal grouped irregularly.</summary>
    private const string IrregularDecimalSource = """
        public sealed class C
        {
            public int M() => {|SST1119:1_000_00_000|};
        }
        """;

    /// <summary>The decimal literal after regrouping.</summary>
    private const string IrregularDecimalFixed = """
        public sealed class C
        {
            public int M() => 100_000_000;
        }
        """;

    /// <summary>A hexadecimal literal grouped irregularly.</summary>
    private const string IrregularHexSource = """
        public sealed class C
        {
            public int M() => {|SST1119:0xFF_F|};
        }
        """;

    /// <summary>The hexadecimal literal after regrouping.</summary>
    private const string IrregularHexFixed = """
        public sealed class C
        {
            public int M() => 0xFFF;
        }
        """;

    /// <summary>Verifies an irregularly grouped decimal literal is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IrregularDecimalIsReportedAsync()
        => await VerifyGrouping.VerifyAnalyzerAsync(IrregularDecimalSource);

    /// <summary>Verifies an irregularly grouped hexadecimal literal is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IrregularHexIsReportedAsync()
        => await VerifyGrouping.VerifyAnalyzerAsync(IrregularHexSource);

    /// <summary>Verifies an evenly grouped decimal literal is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EvenDecimalIsCleanAsync()
        => await VerifyGrouping.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M() => 1_000_000;
            }
            """);

    /// <summary>Verifies a hexadecimal literal grouped in twos, with a leading separator, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EvenHexWithLeadingSeparatorIsCleanAsync()
        => await VerifyGrouping.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M() => 0x_FF_FF;
            }
            """);

    /// <summary>Verifies a literal with no separators is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoSeparatorsIsCleanAsync()
        => await VerifyGrouping.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M() => 1000;
            }
            """);

    /// <summary>Verifies a floating-point literal with separators is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FloatingPointIsCleanAsync()
        => await VerifyGrouping.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public double M() => 1_000.5;
            }
            """);

    /// <summary>Verifies the fix regroups a decimal literal into threes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixRegroupsDecimalAsync()
        => await VerifyFix.VerifyCodeFixAsync(IrregularDecimalSource, IrregularDecimalFixed);

    /// <summary>Verifies the fix regroups a hexadecimal literal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixRegroupsHexAsync()
        => await VerifyFix.VerifyCodeFixAsync(IrregularHexSource, IrregularHexFixed);
}
