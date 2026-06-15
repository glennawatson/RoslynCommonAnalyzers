// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEmptyFinalizer = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.EmptyCodeAnalyzer,
    StyleSharp.Analyzers.EmptyFinalizerCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1434 (empty finalizer) and its fix.</summary>
public class EmptyFinalizerAnalyzerUnitTest
{
    /// <summary>Verifies an empty finalizer is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyFinalizerRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _value;

                                  ~{|SST1434:C|}()
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
        await VerifyEmptyFinalizer.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes every empty finalizer in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class A
                              {
                                  private int _value;

                                  ~{|SST1434:A|}()
                                  {
                                  }
                              }

                              public class B
                              {
                                  private int _value;

                                  ~{|SST1434:B|}()
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
                                   """;
        await VerifyEmptyFinalizer.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a finalizer that does work is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonEmptyFinalizerIsCleanAsync()
        => await VerifyEmptyFinalizer.VerifyAnalyzerAsync(
            """
            public class C
            {
                ~C()
                {
                    System.Console.WriteLine();
                }
            }
            """);
}
