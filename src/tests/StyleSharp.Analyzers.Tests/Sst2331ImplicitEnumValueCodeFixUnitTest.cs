// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyImplicit = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2331ImplicitEnumValueAnalyzer,
    StyleSharp.Analyzers.Sst2331ImplicitEnumValueCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2331ImplicitEnumValueCodeFixProvider"/> (SST2331 assign explicit values).</summary>
public class Sst2331ImplicitEnumValueCodeFixUnitTest
{
    /// <summary>An enum whose every member is implicit.</summary>
    private const string AllImplicitSource = """
        public enum {|SST2331:Color|}
        {
            Red,
            Green,
            Blue,
        }
        """;

    /// <summary>The enum after the fix assigns sequential values.</summary>
    private const string AllImplicitFixed = """
        public enum Color
        {
            Red = 0,
            Green = 1,
            Blue = 2,
        }
        """;

    /// <summary>An enum with one implicit member between explicit ones.</summary>
    private const string PartialSource = """
        public enum {|SST2331:Color|}
        {
            Red = 1,
            Green,
            Blue = 3,
        }
        """;

    /// <summary>The enum after the fix fills only the missing member.</summary>
    private const string PartialFixed = """
        public enum Color
        {
            Red = 1,
            Green = 2,
            Blue = 3,
        }
        """;

    /// <summary>Verifies the fix assigns each member the value the compiler currently gives it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AssignsSequentialValuesAsync()
        => await VerifyImplicit.VerifyCodeFixAsync(AllImplicitSource, AllImplicitFixed);

    /// <summary>Verifies the fix fills only the missing initializers, keeping the ones already written.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FillsOnlyTheMissingMembersAsync()
        => await VerifyImplicit.VerifyCodeFixAsync(PartialSource, PartialFixed);
}
