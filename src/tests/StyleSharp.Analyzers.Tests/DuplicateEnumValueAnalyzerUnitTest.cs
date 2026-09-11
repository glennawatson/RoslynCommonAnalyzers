// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyDuplicateEnumValue = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2455DuplicateEnumValueAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2455DuplicateEnumValueAnalyzer"/> (SST2455).</summary>
public class DuplicateEnumValueAnalyzerUnitTest
{
    /// <summary>Verifies a repeated literal value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RepeatedLiteralValueIsFlaggedAsync() =>
        VerifyDuplicateEnumValue.VerifyAnalyzerAsync(
            """
            internal enum Level
            {
                First = 1,
                {|SST2455:Second|} = 1,
            }
            """);

    /// <summary>Verifies an implicitly numbered member colliding with an explicit one is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ImplicitValueCollidingIsFlaggedAsync() =>
        VerifyDuplicateEnumValue.VerifyAnalyzerAsync(
            """
            internal enum Level
            {
                First = 1,
                Second = 0,
                {|SST2455:Third|},
            }
            """);

    /// <summary>Verifies a third member repeating the same value is reported too.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThirdRepeatIsFlaggedAsync() =>
        VerifyDuplicateEnumValue.VerifyAnalyzerAsync(
            """
            internal enum Level
            {
                First = 1,
                {|SST2455:Second|} = 1,
                {|SST2455:Third|} = 1,
            }
            """);

    /// <summary>Verifies an alias that names the member it duplicates is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DeliberateAliasIsCleanAsync() =>
        VerifyDuplicateEnumValue.VerifyAnalyzerAsync(
            """
            internal enum Level
            {
                None = 0,
                Read = 1,
                Default = Read,
            }
            """);

    /// <summary>Verifies a combination written from the enum's own members is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CombinationOfMembersIsCleanAsync() =>
        VerifyDuplicateEnumValue.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            internal enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
                ReadWrite = Read | Write,
                All = Read | Write,
            }
            """);

    /// <summary>Verifies distinct values are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DistinctValuesAreCleanAsync() =>
        VerifyDuplicateEnumValue.VerifyAnalyzerAsync(
            """
            internal enum Level
            {
                First = 1,
                Second = 2,
            }
            """);

    /// <summary>Verifies a single-member enum is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleMemberEnumIsCleanAsync() =>
        VerifyDuplicateEnumValue.VerifyAnalyzerAsync(
            """
            internal enum Level
            {
                Only = 1,
            }
            """);

    /// <summary>Verifies implicitly numbered members with no explicit values are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ImplicitlyNumberedEnumIsCleanAsync() =>
        VerifyDuplicateEnumValue.VerifyAnalyzerAsync(
            """
            internal enum Level
            {
                First,
                Second,
                Third,
            }
            """);
}
