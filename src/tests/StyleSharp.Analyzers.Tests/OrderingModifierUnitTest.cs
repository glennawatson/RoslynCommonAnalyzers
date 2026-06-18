// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyModifier = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ModifierOrderAnalyzer,
    StyleSharp.Analyzers.ModifierOrderCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the modifier-order rules (SST1206/SST1207).</summary>
public class OrderingModifierUnitTest
{
    /// <summary>Verifies a misordered modifier is reported (SST1206) and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeywordOrderReorderedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  static {|SST1206:public|} int M() => 0;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public static int M() => 0;
                                   }
                                   """;
        await VerifyModifier.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies 'internal' before 'protected' is reported (SST1207) and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtectedBeforeInternalReorderedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  internal {|SST1207:protected|} int M() => 0;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       protected internal int M() => 0;
                                   }
                                   """;
        await VerifyModifier.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All reorders every misordered modifier list in a single document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  static {|SST1206:public|} int M() => 0;

                                  static {|SST1206:public|} int N() => 1;

                                  internal {|SST1207:protected|} int P() => 2;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public static int M() => 0;

                                       public static int N() => 1;

                                       protected internal int P() => 2;
                                   }
                                   """;
        await VerifyModifier.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies modifiers already in canonical order are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CanonicalOrderIsCleanAsync()
        => await VerifyModifier.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public static int M() => 0;
            }
            """);

    /// <summary>Verifies Fix All reorders a type and a member nested inside it in one pass (parent-then-child edits must compose).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllReordersTypeAndNestedMemberAsync()
    {
        const string Source = """
                              static {|SST1206:internal|} class C
                              {
                                  static {|SST1206:public|} int M() => 0;
                              }
                              """;
        const string FixedSource = """
                                   internal static class C
                                   {
                                       public static int M() => 0;
                                   }
                                   """;
        await VerifyModifier.VerifyCodeFixAsync(Source, FixedSource);
    }
}
