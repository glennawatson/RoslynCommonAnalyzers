// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyModifier = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RedundantModifierAnalyzer,
    StyleSharp.Analyzers.RemoveModifierCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1419 (remove redundant modifiers).</summary>
public class RedundantModifierAnalyzerUnitTest
{
    /// <summary>Verifies a single-part partial declaration is reported and fixed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SinglePartialDeclarationIsFixedAsync()
    {
        const string Source = """
                              public {|SST1419:partial|} class C
                              {
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                   }
                                   """;
        await VerifyModifier.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a sealed override in a sealed type is reported and fixed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SealedOverrideInSealedTypeIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public {|SST1419:sealed|} override string ToString() => "C";
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public override string ToString() => "C";
                                   }
                                   """;
        await VerifyModifier.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies genuine partial and sealed-override declarations are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MeaningfulModifiersAreCleanAsync()
        => await VerifyModifier.VerifyAnalyzerAsync(
            """
            public partial class C
            {
            }

            public partial class C
            {
            }

            public class B
            {
                public virtual void M()
                {
                }
            }

            public class D : B
            {
                public sealed override void M()
                {
                }
            }
            """);

    /// <summary>Verifies Fix All removes every redundant single-part partial modifier (SST1419) in one pass.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public {|SST1419:partial|} class A
                              {
                              }

                              public {|SST1419:partial|} class B
                              {
                              }

                              public {|SST1419:partial|} class C
                              {
                              }
                              """;
        const string FixedSource = """
                                   public class A
                                   {
                                   }

                                   public class B
                                   {
                                   }

                                   public class C
                                   {
                                   }
                                   """;
        await VerifyModifier.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes a redundant modifier from a type and one nested inside it in one pass (parent-then-child edits must compose).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixAllRemovesRedundantModifierFromTypeAndNestedTypeAsync()
    {
        const string Source = """
                              public {|SST1419:partial|} class Outer
                              {
                                  private {|SST1419:partial|} class Inner
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class Outer
                                   {
                                       private class Inner
                                       {
                                       }
                                   }
                                   """;
        await VerifyModifier.VerifyCodeFixAsync(Source, FixedSource);
    }
}
