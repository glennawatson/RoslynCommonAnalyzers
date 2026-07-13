// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFlags = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2303MisusedFlagsAttributeAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2303 (flags enums should declare bit values).</summary>
public class Sst2303MisusedFlagsAttributeAnalyzerUnitTest
{
    /// <summary>Verifies a flags enum left to the compiler's counting is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>Nobody wrote <c>Done = 3</c>; the compiler counted to it, and it is now <c>Active | Pending</c>.</remarks>
    [Test]
    public async Task SequentiallyNumberedFlagsEnumIsReportedAsync()
        => await VerifyFlags.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum {|SST2303:Status|}
            {
                None,
                Active,
                Pending,
                Done,
            }
            """);

    /// <summary>Verifies powers of two, with a zero member, are the shape the attribute promises.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PowersOfTwoAreCleanAsync()
        => await VerifyFlags.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
                Execute = 4,
                Delete = 8,
            }
            """);

    /// <summary>Verifies a combination written out of other members is exactly what the attribute is for.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DeclaredCombinationIsCleanAsync()
        => await VerifyFlags.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
                ReadWrite = Read | Write,
                Execute = 4,
                All = Read | Write | Execute,
            }
            """);

    /// <summary>Verifies a combination written as the literal it adds up to is accepted too.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LiteralCombinationIsCleanAsync()
        => await VerifyFlags.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
                ReadWrite = 3,
                Execute = 4,
                All = 7,
            }
            """);

    /// <summary>Verifies a value carrying a bit no member declares can never be a combination, and is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ValueWithAnUndeclaredBitIsReportedAsync()
        => await VerifyFlags.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum {|SST2303:Access|}
            {
                None = 0,
                Read = 1,
                Write = 2,
                Mixed = 5,
            }
            """);

    /// <summary>Verifies an enum with no attribute makes no promise, so its numbering is its own business.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EnumWithoutTheAttributeIsCleanAsync()
        => await VerifyFlags.VerifyAnalyzerAsync(
            """
            public enum Status
            {
                None,
                Active,
                Pending,
                Done,
            }
            """);

    /// <summary>Verifies counted values that happen to land on bits are not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>0, 1 and 2 are a valid flags enum however they were arrived at; the rule reports facts, not luck.</remarks>
    [Test]
    public async Task CountedValuesThatLandOnBitsAreCleanAsync()
        => await VerifyFlags.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Access
            {
                None,
                Read,
                Write,
            }
            """);

    /// <summary>Verifies an unsigned underlying type is read as bits, not as an arithmetic value.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task HighBitOfAnUnsignedEnumIsCleanAsync()
        => await VerifyFlags.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Mask : ulong
            {
                None = 0,
                Low = 1,
                High = 0x8000000000000000,
            }
            """);

    /// <summary>Verifies a negative member is measured on its bits, which are every bit there is.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NegativeMemberIsReportedAsync()
        => await VerifyFlags.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum {|SST2303:Mask|}
            {
                None = 0,
                Low = 1,
                Everything = -1,
            }
            """);

    /// <summary>Verifies the attribute is recognised through its qualified name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task QualifiedAttributeIsRecognisedAsync()
        => await VerifyFlags.VerifyAnalyzerAsync(
            """
            [System.Flags]
            public enum {|SST2303:Status|}
            {
                None,
                Active,
                Pending,
                Done,
            }
            """);
}
