// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyRedundantCtor = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.EmptyCodeAnalyzer,
    StyleSharp.Analyzers.RedundantConstructorCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1433 (redundant constructor) and its fix.</summary>
public class RedundantConstructorAnalyzerUnitTest
{
    /// <summary>Verifies a public parameterless empty sole constructor is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedundantConstructorRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _value;

                                  public {|SST1433:C|}()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private int _value;
                                   }
                                   """;
        await VerifyRedundantCtor.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a private, parameterized, or non-empty constructor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MeaningfulConstructorsAreCleanAsync() =>
        VerifyRedundantCtor.VerifyAnalyzerAsync(
            """
            public class Locked
            {
                private Locked()
                {
                }
            }

            public class WithArgs
            {
                public WithArgs(int value)
                {
                }
            }

            public class TwoConstructors
            {
                public TwoConstructors()
                {
                }

                public TwoConstructors(int value)
                {
                }
            }
            """);

    /// <summary>Verifies Fix All removes every redundant constructor across multiple types in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class A
                              {
                                  private int _value;

                                  public {|SST1433:A|}()
                                  {
                                  }
                              }

                              public class B
                              {
                                  private int _value;

                                  public {|SST1433:B|}()
                                  {
                                  }
                              }

                              public class C
                              {
                                  private int _value;

                                  public {|SST1433:C|}()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class A
                                   {
                                       private int _value;
                                   }

                                   public class B
                                   {
                                       private int _value;
                                   }

                                   public class C
                                   {
                                       private int _value;
                                   }
                                   """;
        await VerifyRedundantCtor.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a struct with a property initializer keeps the constructor those initializers require.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StructWithPropertyInitializerIsCleanAsync() =>
        VerifyRedundantCtor.VerifyAnalyzerAsync(
            """
            public struct Position
            {
                public Position()
                {
                }

                public int Offset { get; set; } = -1;
            }
            """);

    /// <summary>Verifies a struct with a field initializer keeps the constructor those initializers require.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StructWithFieldInitializerIsCleanAsync() =>
        VerifyRedundantCtor.VerifyAnalyzerAsync(
            """
            public struct Position
            {
                public int Offset = -1;

                public Position()
                {
                }
            }
            """);

    /// <summary>Verifies a struct without initializers still reports its redundant constructor.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StructWithoutInitializersStillReportsAsync() =>
        VerifyRedundantCtor.VerifyAnalyzerAsync(
            """
            public struct Position
            {
                public int Offset;

                public {|SST1433:Position|}()
                {
                }
            }
            """);
}
