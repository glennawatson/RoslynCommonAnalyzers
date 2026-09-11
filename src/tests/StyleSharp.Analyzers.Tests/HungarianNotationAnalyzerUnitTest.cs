// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyHungarian = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1305HungarianNotationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the Hungarian-notation rule (SST1305).</summary>
public class HungarianNotationAnalyzerUnitTest
{
    /// <summary>Verifies a Hungarian-notation parameter is reported (SST1305).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task HungarianParameterReportedAsync() =>
        VerifyHungarian.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(int {|SST1305:iCount|})
                {
                }
            }
            """);

    /// <summary>Verifies an ordinary camelCase parameter is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CamelCaseParameterIsCleanAsync() =>
        VerifyHungarian.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(int isEnabled)
                {
                }
            }
            """);

    /// <summary>Verifies an abbreviated technology name is not read as a type prefix.</summary>
    /// <param name="name">The parameter name under test.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks><c>jsRuntime</c> says which runtime it is, the way <c>dbContext</c> says which context.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("jsRuntime")]
    [Arguments("efQueryable")]
    [Arguments("gcType")]
    [Arguments("msTest")]
    public Task AbbreviatedTechnologyNameIsCleanAsync(string name) =>
        VerifyHungarian.VerifyAnalyzerAsync(
            $$"""
            internal class C
            {
                private void M(int {{name}})
                {
                }
            }
            """);

    /// <summary>Verifies a prefix outside the built-in allow-list is reported when not configured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnconfiguredPrefixReportedAsync() =>
        VerifyHungarian.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int {|SST1305:vmCount|};
            }
            """);

    /// <summary>Verifies the rule-specific editorconfig allow-list suppresses a configured prefix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleSpecificAllowedPrefixIsCleanAsync()
    {
        var test = new VerifyHungarian.Test
        {
            TestCode = """
                       internal class C
                       {
                           private int vmCount;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1305.allowed_hungarian_prefixes = vm, wpf

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the general editorconfig allow-list suppresses a configured prefix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralAllowedPrefixIsCleanAsync()
    {
        var test = new VerifyHungarian.Test
        {
            TestCode = """
                       internal class C
                       {
                           private int vmCount;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.allowed_hungarian_prefixes = vm

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
