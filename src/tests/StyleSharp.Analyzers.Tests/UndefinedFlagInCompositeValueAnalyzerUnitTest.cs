// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUndefinedFlag = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2461UndefinedFlagInCompositeValueAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2461UndefinedFlagInCompositeValueAnalyzer"/> (SST2461).</summary>
public class UndefinedFlagInCompositeValueAnalyzerUnitTest
{
    /// <summary>Verifies a composite setting a bit no member declares is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UndefinedBitIsFlaggedAsync()
        => await VerifyUndefinedFlag.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            internal enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
                {|SST2461:All|} = 7,
            }
            """);

    /// <summary>Verifies a composite made only of declared bits is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeclaredBitsAreCleanAsync()
        => await VerifyUndefinedFlag.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            internal enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
                Execute = 4,
                All = 7,
            }
            """);

    /// <summary>Verifies a composite written from the members themselves is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompositeFromMembersIsCleanAsync()
        => await VerifyUndefinedFlag.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            internal enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
                ReadWrite = Read | Write,
            }
            """);

    /// <summary>Verifies an enum without the flags attribute is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonFlagsEnumIsCleanAsync()
        => await VerifyUndefinedFlag.VerifyAnalyzerAsync(
            """
            internal enum Level
            {
                None = 0,
                Read = 1,
                Write = 2,
                All = 7,
            }
            """);

    /// <summary>Verifies single-bit members are never reported, however sparse the enum is.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleBitMembersAreCleanAsync()
        => await VerifyUndefinedFlag.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            internal enum Access
            {
                None = 0,
                Read = 1,
                Reserved = 64,
            }
            """);
}
