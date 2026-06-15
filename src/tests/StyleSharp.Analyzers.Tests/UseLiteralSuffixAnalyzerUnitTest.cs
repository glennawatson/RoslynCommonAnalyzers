// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLiteralSuffix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1139UseLiteralSuffixAnalyzer,
    StyleSharp.Analyzers.Sst1139UseLiteralSuffixCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the literal-suffix rule (SST1139).</summary>
public class UseLiteralSuffixAnalyzerUnitTest
{
    /// <summary>Verifies a cast on an integer literal is reported (SST1139) and replaced with a suffix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IntegerCastReplacedWithSuffixAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private long M() => {|SST1139:(long)1|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private long M() => 1L;
                                   }
                                   """;
        await VerifyLiteralSuffix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every flagged cast in a single document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private long A() => {|SST1139:(long)1|};

                                  private long B() => {|SST1139:(long)2|};

                                  private long D() => {|SST1139:(long)3|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private long A() => 1L;

                                       private long B() => 2L;

                                       private long D() => 3L;
                                   }
                                   """;
        await VerifyLiteralSuffix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a suffixed literal and a non-suffixable cast are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SuffixedLiteralAndIntCastAreCleanAsync()
        => await VerifyLiteralSuffix.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private long M() => 1L;

                private int N() => (int)1;

                private long L() => (long)1.5.GetHashCode();
            }
            """);
}
