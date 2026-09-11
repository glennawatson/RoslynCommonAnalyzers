// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyForeach = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1467UseForeachOverManualEnumeratorAnalyzer,
    StyleSharp.Analyzers.Sst1467UseForeachOverManualEnumeratorCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1467 (enumerate with foreach instead of driving the enumerator by hand) and its fix.</summary>
public class UseForeachOverManualEnumeratorAnalyzerUnitTest
{
    /// <summary>Source with two loops driving an enumerator by hand, one naming the current element and one reading it inline.</summary>
    private const string TwoManualEnumeratorLoopsSource = """
        using System.Collections.Generic;

        public class C
        {
            public int Sum(List<int> values)
            {
                var total = 0;
                var e = values.GetEnumerator();
                {|SST1467:while|} (e.MoveNext())
                {
                    var value = e.Current;
                    total += value;
                }

                return total;
            }

            public int TotalLength(List<string> values)
            {
                var total = 0;
                var e = values.GetEnumerator();
                {|SST1467:while|} (e.MoveNext())
                {
                    total += e.Current.Length;
                }

                return total;
            }
        }
        """;

    /// <summary>The same two loops once both drive the enumeration with foreach.</summary>
    private const string TwoManualEnumeratorLoopsFixedSource = """
        using System.Collections.Generic;

        public class C
        {
            public int Sum(List<int> values)
            {
                var total = 0;
                foreach (var value in values)
                {
                    total += value;
                }

                return total;
            }

            public int TotalLength(List<string> values)
            {
                var total = 0;
                foreach (var item in values)
                {
                    total += item.Length;
                }

                return total;
            }
        }
        """;

    /// <summary>Verifies a declaration separated from its loop by a region is not rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The declaration is deleted and the loop rewritten in its place, so a directive between the two
    /// would lose the half that travels with the statement that goes.
    /// </remarks>
    [Test]
    public async Task DeclarationAcrossADirectiveIsNotRewrittenAsync()
    {
        const string Source = """
            using System.Collections.Generic;

            public sealed class C
            {
                public int M(List<int> items)
                {
                    var total = 0;
                    var enumerator = items.GetEnumerator();
            #region Walk
                    {|SST1467:while|} (enumerator.MoveNext())
                    {
                        total += enumerator.Current;
                    }
            #endregion
                    return total;
                }
            }
            """;
        await VerifyForeach.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies the canonical pattern is reported and the fix reuses the body's own declaration as the iteration variable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CanonicalLoopIsRewrittenWithBodyDeclarationAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int Sum(List<int> values)
                                  {
                                      var total = 0;
                                      var e = values.GetEnumerator();
                                      {|SST1467:while|} (e.MoveNext())
                                      {
                                          var value = e.Current;
                                          total += value;
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public int Sum(List<int> values)
                                       {
                                           var total = 0;
                                           foreach (var value in values)
                                           {
                                               total += value;
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await VerifyForeach.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies scattered Current reads are rewritten onto an introduced 'item' iteration variable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ScatteredCurrentReadsAreRewrittenWithItemVariableAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int TotalLength(List<string> values)
                                  {
                                      var total = 0;
                                      var e = values.GetEnumerator();
                                      {|SST1467:while|} (e.MoveNext())
                                      {
                                          if (e.Current.Length > 0)
                                          {
                                              total += e.Current.Length;
                                          }
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public int TotalLength(List<string> values)
                                       {
                                           var total = 0;
                                           foreach (var item in values)
                                           {
                                               if (item.Length > 0)
                                               {
                                                   total += item.Length;
                                               }
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await VerifyForeach.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an enumerator that is still used after the loop is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EnumeratorUsedAfterLoopIsCleanAsync() =>
        VerifyForeach.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public int Sum(List<int> values)
                {
                    var total = 0;
                    var e = values.GetEnumerator();
                    while (e.MoveNext())
                    {
                        total += e.Current;
                    }

                    e.Dispose();
                    return total;
                }
            }
            """);

    /// <summary>Verifies a body that uses the enumerator for anything besides reading Current is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DisposeInsideBodyIsCleanAsync() =>
        VerifyForeach.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public int Sum(List<int> values)
                {
                    var total = 0;
                    var e = values.GetEnumerator();
                    while (e.MoveNext())
                    {
                        total += e.Current;
                        e.Dispose();
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies an enumerator held by a using declaration is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingDeclarationEnumeratorIsCleanAsync() =>
        VerifyForeach.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public int Sum(List<int> values)
                {
                    var total = 0;
                    using var e = values.GetEnumerator();
                    while (e.MoveNext())
                    {
                        total += e.Current;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies a loop separated from the enumerator declaration by another statement is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoopNotImmediatelyAfterDeclarationIsCleanAsync() =>
        VerifyForeach.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public int Sum(List<int> values)
                {
                    var e = values.GetEnumerator();
                    var total = 0;
                    while (e.MoveNext())
                    {
                        total += e.Current;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies the diagnostic is still reported when 'item' is already taken, even though no fix is offered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ItemAlreadyInScopeReportsWithoutFixAsync() =>
        VerifyForeach.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public int Offset(List<int> values, int item)
                {
                    var total = 0;
                    var e = values.GetEnumerator();
                    {|SST1467:while|} (e.MoveNext())
                    {
                        total += e.Current + item;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies Fix All rewrites every manual-enumerator loop in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixAllRewritesEveryLoopAsync() =>
        VerifyForeach.VerifyCodeFixAsync(TwoManualEnumeratorLoopsSource, TwoManualEnumeratorLoopsFixedSource);
}
