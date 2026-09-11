// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyZero = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2329FlagsEnumMissingZeroValueAnalyzer,
    StyleSharp.Analyzers.Sst2329FlagsEnumMissingZeroValueCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2329FlagsEnumMissingZeroValueCodeFixProvider"/> (SST2329 add a zero value).</summary>
public class Sst2329FlagsEnumMissingZeroValueCodeFixUnitTest
{
    /// <summary>A flags enum with explicit bit values but no zero.</summary>
    private const string ExplicitSource = """
        using System;

        [Flags]
        public enum {|SST2329:Access|}
        {
            Read = 1,
            Write = 2,
        }
        """;

    /// <summary>The enum after the fix adds a zero member.</summary>
    private const string ExplicitFixed = """
        using System;

        [Flags]
        public enum Access
        {
            None = 0,
            Read = 1,
            Write = 2,
        }
        """;

    /// <summary>A three-member flags enum with no zero.</summary>
    private const string ThreeMemberSource = """
        using System;

        [Flags]
        public enum {|SST2329:Access|}
        {
            Read = 1,
            Write = 2,
            Execute = 4,
        }
        """;

    /// <summary>The three-member enum after the fix.</summary>
    private const string ThreeMemberFixed = """
        using System;

        [Flags]
        public enum Access
        {
            None = 0,
            Read = 1,
            Write = 2,
            Execute = 4,
        }
        """;

    /// <summary>Verifies the fix inserts <c>None = 0</c> as the first member.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AddsNoneMemberAsync()
        => await VerifyZero.VerifyCodeFixAsync(ExplicitSource, ExplicitFixed);

    /// <summary>Verifies the fix inserts the zero member ahead of the other members.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AddsNoneBeforeOtherMembersAsync()
        => await VerifyZero.VerifyCodeFixAsync(ThreeMemberSource, ThreeMemberFixed);

    /// <summary>Verifies an enum whose body carries a directive is reported but not edited.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Members inside an inactive <c>#if</c> are disabled text, not members, so the fix would read this
    /// enum as empty and rewrite its close brace — and the region, with everything in it, is the trivia
    /// that brace carries.
    /// </remarks>
    [Test]
    public async Task EnumWithADirectiveInItsBodyIsNotEditedAsync()
    {
        const string Source = """
            using System;

            [Flags]
            public enum {|SST2329:Access|}
            {
            #if FULL
                Read = 1,
                Write = 2,
            #endif
            }
            """;
        await VerifyZero.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies the first member's documentation is not copied onto the new one.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DocumentationOfTheFirstMemberIsNotCopiedAsync()
    {
        const string Source = """
            using System;

            [Flags]
            public enum {|SST2329:Access|}
            {
                /// <summary>Permission to read.</summary>
                Read = 1,
            }
            """;
        const string FixedSource = """
            using System;

            [Flags]
            public enum Access
            {
                None = 0,
                /// <summary>Permission to read.</summary>
                Read = 1,
            }
            """;
        await VerifyZero.VerifyCodeFixAsync(Source, FixedSource);
    }
}
