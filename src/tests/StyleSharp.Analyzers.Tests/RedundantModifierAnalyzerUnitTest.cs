// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using RoslynCommon.Analyzers.Tests;
using VerifyModifier = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RedundantModifierAnalyzer,
    StyleSharp.Analyzers.RemoveModifierCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1419 (remove redundant modifiers).</summary>
public class RedundantModifierAnalyzerUnitTest
{
    /// <summary>Verifies the defining and implementing parts both require their partial modifiers.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PartialMethodPartsAreCleanAsync() =>
        VerifyModifier.VerifyAnalyzerAsync("""
            public partial class C
            {
                public partial int M();
            }

            public partial class C
            {
                public partial int M() => 1;
            }
            """);

    /// <summary>Verifies a native partial implementation preserves the declaration's required modifier.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NativePartialMethodPartsAreCleanAsync() =>
        new VerifyModifier.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net80,
            TestCode = """
            using System.Runtime.InteropServices;

            public static partial class NativeMethods
            {
                [LibraryImport("native")]
                public static partial int GetValue();
            }

            public static partial class NativeMethods
            {
                [DllImport("native")]
                public static extern partial int GetValue();
            }
            """,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies an optional partial method also requires its containing type to be partial.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OptionalPartialMethodIsCleanAsync() =>
        VerifyModifier.VerifyAnalyzerAsync("""
            public partial class C
            {
                partial void OnChanged();
            }
            """);

    /// <summary>Verifies nested partial type parts do not make the containing type partial.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NestedPartialTypePartsDoNotKeepContainingTypePartialAsync() =>
        VerifyModifier.VerifyAnalyzerAsync("""
            public {|SST1419:partial|} class Outer
            {
                private partial class Inner
                {
                }

                private partial class Inner
                {
                }
            }
            """);

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MeaningfulModifiersAreCleanAsync() =>
        VerifyModifier.VerifyAnalyzerAsync(
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

    /// <summary>Verifies a sealed default interface member is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SealedDefaultInterfaceMemberIsCleanAsync() =>
        VerifyModifier.VerifyAnalyzerAsync(
            """
            using System;

            public class Test
            {
                public void Run()
                {
                    I i = new C();
                    i.M();
                    i.N();
                }
            }

            interface I
            {
                sealed void M() => Console.WriteLine("I.M");
                void N() => Console.WriteLine("I.N");
            }

            class C : I
            {
                public void M() => Console.WriteLine("C.M");
                public void N() => Console.WriteLine("C.N");
            }
            """);

    /// <summary>Verifies a sealed static interface member is still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// A static interface member is non-virtual unless it is declared <c>static virtual</c> or
    /// <c>static abstract</c>, so <c>sealed</c> seals nothing on one. Skipping every member of an interface
    /// would trade the default-implementation false positive for silence here.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SealedStaticInterfaceMemberIsReportedAsync() =>
        VerifyModifier.VerifyAnalyzerAsync(
            """
            using System;

            interface I
            {
                static {|SST1419:sealed|} void S() => Console.WriteLine("I.S");
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

    /// <summary>Verifies Fix All composes redundant modifiers on a sealed type and one of its members.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixAllRemovesRedundantModifiersFromSealedTypeAndMemberAsync()
    {
        const string Source = """
                              public sealed {|SST1419:partial|} class C
                              {
                                  {|SST1427:protected|} void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       void M()
                                       {
                                       }
                                   }
                                   """;
        await VerifyModifier.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a <c>checked</c> block that guards no arithmetic is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RedundantCheckedStatementIsReportedAsync() =>
        VerifyModifier.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M(int a)
                {
                    {|SST1419:checked|}
                    {
                        System.Console.WriteLine(a);
                    }
                }
            }
            """);

    /// <summary>Verifies an <c>unchecked</c> block that guards no arithmetic is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RedundantUncheckedStatementIsReportedAsync() =>
        VerifyModifier.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M(string s)
                {
                    {|SST1419:unchecked|}
                    {
                        System.Console.WriteLine(s);
                    }
                }
            }
            """);

    /// <summary>Verifies a <c>checked</c> expression that wraps no arithmetic is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RedundantCheckedExpressionIsReportedAsync() =>
        VerifyModifier.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int a) => {|SST1419:checked|}(a);
            }
            """);

    /// <summary>Verifies a <c>checked</c> block whose arithmetic could overflow is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CheckedBlockGuardingArithmeticIsCleanAsync() =>
        VerifyModifier.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int a, int b)
                {
                    checked
                    {
                        return a + b;
                    }
                }
            }
            """);

    /// <summary>Verifies a <c>checked</c> expression over a multiplication is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CheckedExpressionOverMultiplicationIsCleanAsync() =>
        VerifyModifier.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int a) => checked(a * 2);
            }
            """);

    /// <summary>Verifies an <c>unchecked</c> expression over a narrowing cast is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>A narrowing numeric conversion is exactly what an overflow-check context changes.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UncheckedExpressionOverCastIsCleanAsync() =>
        VerifyModifier.VerifyAnalyzerAsync(
            """
            public class C
            {
                public ulong M(long n) => unchecked((ulong)n);
            }
            """);
}
