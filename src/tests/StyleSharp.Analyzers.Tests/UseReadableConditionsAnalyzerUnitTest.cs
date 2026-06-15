// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyReadableConditions = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1131UseReadableConditionsAnalyzer,
    StyleSharp.Analyzers.Sst1131UseReadableConditionsCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the readable-conditions rule (SST1131).</summary>
public class UseReadableConditionsAnalyzerUnitTest
{
    /// <summary>Verifies a yoda equality is reported (SST1131) and the operands swapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task YodaEqualitySwappedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private static bool M(int count) => {|SST1131:0 == count|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private static bool M(int count) => count == 0;
                                   }
                                   """;
        await VerifyReadableConditions.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a yoda less-than is reported (SST1131) and rewritten with a flipped operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task YodaRelationalFlippedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private static bool M(int count) => {|SST1131:0 < count|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private static bool M(int count) => count > 0;
                                   }
                                   """;
        await VerifyReadableConditions.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every yoda condition in a document in a single pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private static bool First(int count) => {|SST1131:0 == count|};

                                  private static bool Second(int count) => {|SST1131:0 < count|};

                                  private static bool Third(int count) => {|SST1131:0 == count|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private static bool First(int count) => count == 0;

                                       private static bool Second(int count) => count > 0;

                                       private static bool Third(int count) => count == 0;
                                   }
                                   """;
        await VerifyReadableConditions.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a natural comparison and a literal-only comparison are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NaturalAndLiteralComparisonsAreCleanAsync()
        => await VerifyReadableConditions.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static bool M(int count) => count == 0;

                private static bool N() => 1 == 2;
            }
            """);
}
