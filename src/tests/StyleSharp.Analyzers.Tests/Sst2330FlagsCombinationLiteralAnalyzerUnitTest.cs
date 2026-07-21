// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCombination = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2330FlagsCombinationLiteralAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2330 (write a combined flags value as the members it combines).</summary>
public class Sst2330FlagsCombinationLiteralAnalyzerUnitTest
{
    /// <summary>Verifies a literal equal to the OR of every single bit is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LiteralCombiningEveryBitIsReportedAsync()
        => await VerifyCombination.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
                Execute = 4,
                All = {|SST2330:7|},
            }
            """);

    /// <summary>Verifies a literal equal to two of the single bits is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LiteralCombiningTwoBitsIsReportedAsync()
        => await VerifyCombination.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
                ReadWrite = {|SST2330:3|},
            }
            """);

    /// <summary>Verifies a value written as the OR of its members is already self-documenting and clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ValueWrittenAsMembersIsCleanAsync()
        => await VerifyCombination.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
                Execute = 4,
                All = Read | Write | Execute,
            }
            """);

    /// <summary>Verifies a single-bit member is a flag definition, not a combination, and is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SingleBitMemberIsCleanAsync()
        => await VerifyCombination.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
                Execute = 8,
            }
            """);

    /// <summary>Verifies a literal carrying a bit no member declares is not a clean combination and is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LiteralWithUndeclaredBitIsCleanAsync()
        => await VerifyCombination.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
                Mixed = 5,
            }
            """);

    /// <summary>Verifies an enum without the attribute makes no combination promise and is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EnumWithoutTheAttributeIsCleanAsync()
        => await VerifyCombination.VerifyAnalyzerAsync(
            """
            public enum Access
            {
                Read = 1,
                Write = 2,
                Both = 3,
            }
            """);
}
