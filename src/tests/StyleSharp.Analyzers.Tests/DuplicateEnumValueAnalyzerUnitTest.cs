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
    /// <summary>Checks integer literal representations preserve duplicate detection and increasing sequences.</summary>
    /// <param name="type">The enum underlying type.</param>
    /// <param name="first">The first value.</param>
    /// <param name="second">A distinct following value.</param>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("uint", "1U", "2U")]
    [Arguments("long", "1L", "2L")]
    [Arguments("ulong", "1UL", "2UL")]
    [Arguments("ulong", "9223372036854775808UL", "9223372036854775809UL")]
    [Arguments("int", "-2", "-1")]
    [Arguments("int", "+1", "+2")]
    [Arguments("int", "~2", "~1")]
    public Task IntegerLiteralFormsRetainDuplicateSemanticsAsync(string type, string first, string second) =>
        VerifyDuplicateEnumValue.VerifyAnalyzerAsync($$"""
            enum Distinct : {{type}} { A = {{first}}, B = {{second}} }
            enum Duplicate : {{type}} { A = {{first}}, {|SST2455:B|} = {{first}} }
            """);

    /// <summary>Checks invalid declarations retain the compiler's recovered constant semantics for later members.</summary>
    /// <param name="members">The malformed enum members.</param>
    /// <returns>The verification task.</returns>
    [Test]
    [Arguments("Broken = Missing, A = 1, {|SST2455:B|} = 1")]
    [Arguments("Broken = 1.0, {|SST2455:A|} = 1, {|SST2455:B|} = 1")]
    [Arguments("A = 9223372036854775807L, Overflow, {|SST2455:B|} = 9223372036854775807L")]
    public async Task InvalidDeclarationsRetainCompilerConstantSemanticsAsync(string members)
    {
        var test = new VerifyDuplicateEnumValue.Test { TestCode = $"enum E : long {{ {members} }}", CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.None };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Checks external names in a constant expression are not mistaken for explicit sibling aliases.</summary>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExternalConstantExpressionStillReportsDuplicatesAsync() =>
        VerifyDuplicateEnumValue.VerifyAnalyzerAsync("""
            static class Constants { public const int Value = 1; }
            enum E { A = 1, {|SST2455:B|} = Constants.Value, Alias = E.A }
            """);

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
