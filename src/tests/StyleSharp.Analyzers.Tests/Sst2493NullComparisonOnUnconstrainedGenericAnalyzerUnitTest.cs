// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2493NullComparisonOnUnconstrainedGenericAnalyzer,
    StyleSharp.Analyzers.Sst2493NullComparisonOnUnconstrainedGenericCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2493 (== null on an unconstrained type parameter).</summary>
public class Sst2493NullComparisonOnUnconstrainedGenericAnalyzerUnitTest
{
    /// <summary>Verifies <c>== null</c> on an unconstrained parameter is reported and rewritten to <c>is null</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualsNullIsRewrittenToIsNullAsync()
    {
        const string Source = """
            public sealed class C
            {
                public bool M<T>(T value) => {|SST2493:value == null|};
            }
            """;
        const string Fixed = """
            public sealed class C
            {
                public bool M<T>(T value) => value is null;
            }
            """;
        await Verify.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies <c>!= null</c> on an unconstrained parameter is reported and rewritten to <c>is not null</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NotEqualsNullIsRewrittenToIsNotNullAsync()
    {
        const string Source = """
            public sealed class C
            {
                public bool M<T>(T value) => {|SST2493:value != null|};
            }
            """;
        const string Fixed = """
            public sealed class C
            {
                public bool M<T>(T value) => value is not null;
            }
            """;
        await Verify.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies the null literal on the left is handled and the operand survives the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullOnLeftIsRewrittenAsync()
    {
        const string Source = """
            public sealed class C
            {
                public bool M<T>(T value) => {|SST2493:null == value|};
            }
            """;
        const string Fixed = """
            public sealed class C
            {
                public bool M<T>(T value) => value is null;
            }
            """;
        await Verify.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a class-constrained parameter and a non-generic operand are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstrainedAndNonGenericAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool Reference<T>(T value) where T : class => value == null;
                public bool NotNull<T>(T value) where T : notnull => value.Equals(null);
                public bool Concrete(string value) => value == null;
            }
            """);

    /// <summary>Verifies a base-class constraint pins the parameter to a reference type and is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseClassConstraintIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public bool M<T>(T value) where T : Stream => value == null;
            }
            """);
}
