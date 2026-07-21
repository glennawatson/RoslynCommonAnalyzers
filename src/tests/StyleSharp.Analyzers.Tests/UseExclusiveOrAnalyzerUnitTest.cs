// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyExclusiveOr = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2261UseExclusiveOrAnalyzer,
    StyleSharp.Analyzers.Sst2261UseExclusiveOrCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2261UseExclusiveOrAnalyzer"/> and its code fix (SST2261).</summary>
public class UseExclusiveOrAnalyzerUnitTest
{
    /// <summary>Verifies the logical exclusive-or reimplementation is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LogicalFormIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(bool x, bool y) => {|SST2261:(x && !y) || (!x && y)|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(bool x, bool y) => x ^ y;
                                   }
                                   """;
        await VerifyExclusiveOr.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the bitwise exclusive-or reimplementation is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BitwiseFormIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(bool x, bool y) => {|SST2261:(x & !y) | (!x & y)|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(bool x, bool y) => x ^ y;
                                   }
                                   """;
        await VerifyExclusiveOr.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies member-access operands are reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberAccessOperandsAreFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool A { get; set; }

                                  public bool B { get; set; }

                                  public bool M() => {|SST2261:(A && !B) || (!A && B)|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool A { get; set; }

                                       public bool B { get; set; }

                                       public bool M() => A ^ B;
                                   }
                                   """;
        await VerifyExclusiveOr.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies operands with a side effect are left alone; the long form runs each twice.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SideEffectingOperandsAreCleanAsync()
        => await VerifyExclusiveOr.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private bool P() => true;

                private bool Q() => false;

                public bool M() => (P() && !Q()) || (!P() && Q());
            }
            """);

    /// <summary>Verifies a disjunction that is not a mirror image is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonMirroredDisjunctionIsCleanAsync()
        => await VerifyExclusiveOr.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(bool x, bool y, bool z) => (x && !y) || (!x && z);
            }
            """);
}
