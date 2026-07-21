// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2494ConstantNullCoalesceAnalyzer,
    StyleSharp.Analyzers.Sst2494ConstantNullCoalesceCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2494 (null-coalescing over a constant null).</summary>
public class Sst2494ConstantNullCoalesceAnalyzerUnitTest
{
    /// <summary>Verifies a <c>default(T)</c> left operand is reported and folded to the right operand.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultLeftIsFoldedAsync()
    {
        const string Source = """
            public sealed class C
            {
                public string M(string b) => {|SST2494:default(string) ?? b|};
            }
            """;
        const string Fixed = """
            public sealed class C
            {
                public string M(string b) => b;
            }
            """;
        await Verify.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a constant-null left operand is reported and folded to the right operand.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantNullLeftIsFoldedAsync()
    {
        const string Source = """
            public sealed class C
            {
                private const string N = null;
                public string M(string b) => {|SST2494:N ?? b|};
            }
            """;
        const string Fixed = """
            public sealed class C
            {
                private const string N = null;
                public string M(string b) => b;
            }
            """;
        await Verify.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a cast null left operand is reported and folded to the right operand.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CastNullLeftIsFoldedAsync()
    {
        const string Source = """
            public sealed class C
            {
                public string M(string b) => {|SST2494:(string)null ?? b|};
            }
            """;
        const string Fixed = """
            public sealed class C
            {
                public string M(string b) => b;
            }
            """;
        await Verify.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a non-constant left operand is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantLeftIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string a, string b) => a ?? b;
            }
            """);
}
