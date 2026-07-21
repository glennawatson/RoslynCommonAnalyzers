// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyZero = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2329FlagsEnumMissingZeroValueAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2329 (flags enums should declare a zero value).</summary>
public class Sst2329FlagsEnumMissingZeroValueAnalyzerUnitTest
{
    /// <summary>Verifies a flags enum with no zero-valued member is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FlagsEnumWithoutZeroIsReportedAsync()
        => await VerifyZero.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum {|SST2329:Access|}
            {
                Read = 1,
                Write = 2,
            }
            """);

    /// <summary>Verifies a flags enum whose zero value is written as <c>None = 0</c> is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FlagsEnumWithNoneIsCleanAsync()
        => await VerifyZero.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
            }
            """);

    /// <summary>Verifies a zero value under any name, not only <c>None</c>, is accepted.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FlagsEnumWithZeroUnderAnotherNameIsCleanAsync()
        => await VerifyZero.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Access
            {
                Empty = 0,
                Read = 1,
            }
            """);

    /// <summary>Verifies an enum without the attribute is never reported, whatever its values.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EnumWithoutTheAttributeIsCleanAsync()
        => await VerifyZero.VerifyAnalyzerAsync(
            """
            public enum Access
            {
                Read = 1,
                Write = 2,
            }
            """);

    /// <summary>Verifies the implicit zero of a default-numbered flags enum still counts as a zero value.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The first member of a default-numbered enum is zero, so the empty set already has a name.</remarks>
    [Test]
    public async Task DefaultNumberedFirstMemberIsZeroAndCleanAsync()
        => await VerifyZero.VerifyAnalyzerAsync(
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
}
