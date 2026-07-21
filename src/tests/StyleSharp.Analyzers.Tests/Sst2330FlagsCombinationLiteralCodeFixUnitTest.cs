// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCombination = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2330FlagsCombinationLiteralAnalyzer,
    StyleSharp.Analyzers.Sst2330FlagsCombinationLiteralCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2330FlagsCombinationLiteralCodeFixProvider"/> (SST2330 name the combined flags).</summary>
public class Sst2330FlagsCombinationLiteralCodeFixUnitTest
{
    /// <summary>A three-bit combination written as the literal it adds up to.</summary>
    private const string ThreeBitSource = """
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
        """;

    /// <summary>The three-bit combination after the fix names its members.</summary>
    private const string ThreeBitFixed = """
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
        """;

    /// <summary>A two-bit combination written as a literal.</summary>
    private const string TwoBitSource = """
        using System;

        [Flags]
        public enum Access
        {
            None = 0,
            Read = 1,
            Write = 2,
            ReadWrite = {|SST2330:3|},
        }
        """;

    /// <summary>The two-bit combination after the fix.</summary>
    private const string TwoBitFixed = """
        using System;

        [Flags]
        public enum Access
        {
            None = 0,
            Read = 1,
            Write = 2,
            ReadWrite = Read | Write,
        }
        """;

    /// <summary>Verifies the fix rewrites the literal into the OR of every member it combines.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RewritesLiteralToMemberOrAsync()
        => await VerifyCombination.VerifyCodeFixAsync(ThreeBitSource, ThreeBitFixed);

    /// <summary>Verifies the fix names two members for a two-bit combination.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RewritesTwoBitLiteralAsync()
        => await VerifyCombination.VerifyCodeFixAsync(TwoBitSource, TwoBitFixed);
}
