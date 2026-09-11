// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2234NullableShorthandAnalyzer,
    StyleSharp.Analyzers.Sst2234NullableShorthandCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2234NullableShorthandAnalyzer"/> (SST2234 Nullable&lt;T&gt; shorthand).</summary>
public class Sst2234NullableShorthandAnalyzerUnitTest
{
    /// <summary>An explicit Nullable&lt;int&gt; field type.</summary>
    private const string ExplicitFieldSource = """
        using System;

        public class C
        {
            private {|SST2234:Nullable<int>|} _value;

            public int Get() => _value ?? 0;
        }
        """;

    /// <summary>The explicit field source after the fix.</summary>
    private const string ExplicitFieldFixed = """
        using System;

        public class C
        {
            private int? _value;

            public int Get() => _value ?? 0;
        }
        """;

    /// <summary>A fully qualified spelling.</summary>
    private const string QualifiedSource = """
        public class C
        {
            public System.{|SST2234:Nullable<int>|} M() => null;
        }
        """;

    /// <summary>The qualified source after the fix.</summary>
    private const string QualifiedFixed = """
        public class C
        {
            public int? M() => null;
        }
        """;

    /// <summary>Verifies an explicit Nullable&lt;T&gt; spelling is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExplicitNullableSpellingIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(ExplicitFieldSource);

    /// <summary>Verifies the shorthand itself is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ShorthandIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int? _value;

                public int Get() => _value ?? 0;
            }
            """);

    /// <summary>Verifies an unbound generic typeof is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnboundTypeofIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public Type M() => typeof(Nullable<>);
            }
            """);

    /// <summary>Verifies nameof operands are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NameofOperandIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public string M() => nameof(Nullable<int>);
            }
            """);

    /// <summary>Verifies the non-generic Nullable helper class is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullableHelperClassIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public Type M(Type type) => Nullable.GetUnderlyingType(type);
            }
            """);

    /// <summary>Verifies a nested type-argument spelling is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NestedTypeArgumentIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public List<{|SST2234:Nullable<int>|}> M() => [];
            }
            """);

    /// <summary>Verifies a spelling inside a documentation reference is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>A <c>cref</c> has no <c>?</c> shorthand, so the long spelling is the only one that binds.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CrefSpellingIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                /// <summary>Reads a <see cref="Nullable{Int32}"/> value.</summary>
                /// <returns>The value.</returns>
                public int? M() => null;
            }
            """);

    /// <summary>Verifies the fix rewrites a plain spelling.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixRewritesPlainSpellingAsync() =>
        Verify.VerifyCodeFixAsync(ExplicitFieldSource, ExplicitFieldFixed);

    /// <summary>Verifies the fix rewrites a qualified spelling without leaving the qualifier.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixRewritesQualifiedSpellingAsync() =>
        Verify.VerifyCodeFixAsync(QualifiedSource, QualifiedFixed);
}
