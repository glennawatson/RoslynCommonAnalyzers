// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNamedArgumentOrder = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1220NamedArgumentOrderAnalyzer,
    StyleSharp.Analyzers.Sst1220NamedArgumentOrderCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the named-argument order rule (SST1220) and its reorder fix.</summary>
public class NamedArgumentOrderAnalyzerUnitTest
{
    /// <summary>Verifies an out-of-order all-named call is reported and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReorderedInvocationReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private static void M(int a, int b)
                                  {
                                  }

                                  private static void Caller() => M{|SST1220:(b: 1, a: 2)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private static void M(int a, int b)
                                       {
                                       }

                                       private static void Caller() => M(a: 2, b: 1);
                                   }
                                   """;
        await VerifyNamedArgumentOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an out-of-order all-named object creation is reported and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReorderedObjectCreationReportedAsync()
    {
        const string Source = """
                              public class Point
                              {
                                  public Point(int x, int y)
                                  {
                                  }

                                  public static Point Make() => new Point{|SST1220:(y: 2, x: 1)|};
                              }
                              """;
        const string FixedSource = """
                                   public class Point
                                   {
                                       public Point(int x, int y)
                                       {
                                       }

                                       public static Point Make() => new Point(x: 1, y: 2);
                                   }
                                   """;
        await VerifyNamedArgumentOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies all-named arguments already in declaration order are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InOrderArgumentsAreCleanAsync()
        => await VerifyNamedArgumentOrder.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static void M(int a, int b)
                {
                }

                private static void Caller() => M(a: 1, b: 2);
            }
            """);

    /// <summary>Verifies a call that is not fully named is not reported, even when out of order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartiallyNamedCallIsCleanAsync()
        => await VerifyNamedArgumentOrder.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static void M(int a, int b)
                {
                }

                private static void Caller() => M(1, b: 2);
            }
            """);
}
