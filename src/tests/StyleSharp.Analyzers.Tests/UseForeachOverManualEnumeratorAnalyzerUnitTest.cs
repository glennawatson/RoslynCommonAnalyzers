// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

    /// <summary>Verifies the condition must be a zero-argument MoveNext call on a local name.</summary>
    /// <param name="condition">The loop condition.</param>
    /// <param name="expected">Whether the condition identifies the enumerator.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("e.MoveNext()", true)]
    [Arguments("true", false)]
    [Arguments("e.MoveNext(1)", false)]
    [Arguments("MoveNext()", false)]
    [Arguments("this.e.MoveNext()", false)]
    [Arguments("e.Next()", false)]
    [Arguments("e.MoveNext<int>()", false)]
    [Arguments("e->MoveNext()", false)]
    public async Task ConditionMustNameEnumeratorAsync(string condition, bool expected)
    {
        var loop = (WhileStatementSyntax)SyntaxFactory.ParseStatement($"while ({condition}) {{ }}");
        var result = Sst1467UseForeachOverManualEnumeratorAnalyzer.TryGetEnumeratorName(loop, out var name);
        await Assert.That(result).IsEqualTo(expected);
        await Assert.That(name).IsEqualTo(expected ? "e" : string.Empty);
    }

    /// <summary>Verifies declarations must immediately initialize one plain local from GetEnumerator.</summary>
    /// <param name="declaration">The statement preceding the loop.</param>
    /// <param name="expected">Whether the declaration can be converted.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("var e = values.GetEnumerator();", true)]
    [Arguments("", false)]
    [Arguments("M();", false)]
    [Arguments("using var e = values.GetEnumerator();", false)]
    [Arguments("const int e = 0;", false)]
    [Arguments("E e = values.GetEnumerator(), other = e;", false)]
    [Arguments("var other = values.GetEnumerator();", false)]
    [Arguments("E e;", false)]
    [Arguments("var e = values;", false)]
    [Arguments("var e = values.GetEnumerator(1);", false)]
    [Arguments("var e = GetEnumerator();", false)]
    [Arguments("var e = values.Other();", false)]
    [Arguments("var e = values.GetEnumerator<int>();", false)]
    [Arguments("var e = values->GetEnumerator();", false)]
    public async Task DeclarationMustInitializeSingleEnumeratorAsync(string declaration, bool expected)
    {
        var block = (BlockSyntax)SyntaxFactory.ParseStatement($"{{ {declaration} while (e.MoveNext()) {{ }} }}");
        var loop = block.Statements.OfType<WhileStatementSyntax>().Single();
        var result = Sst1467UseForeachOverManualEnumeratorAnalyzer.TryGetEnumeratorDeclaration(loop, "e", out var found, out var source);
        await Assert.That(result).IsEqualTo(expected);
        await Assert.That(found is not null).IsEqualTo(expected);
        await Assert.That(source?.ToString()).IsEqualTo(expected ? "values" : null);
    }

    /// <summary>Verifies Current reads are accepted while mutation, aliasing and name collisions are rejected.</summary>
    /// <param name="body">The statements inside the loop.</param>
    /// <param name="expected">Whether the body can become a foreach body.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Use(e.Current);", true)]
    [Arguments("Use(in e.Current);", true)]
    [Arguments("var x = e.Current!;", true)]
    [Arguments("var x = -e.Current;", true)]
    [Arguments("e.Current++;", false)]
    [Arguments("e.Current--;", false)]
    [Arguments("++e.Current;", false)]
    [Arguments("--e.Current;", false)]
    [Arguments("var p = &e.Current;", false)]
    [Arguments("Use(ref e.Current);", false)]
    [Arguments("Use(out e.Current);", false)]
    [Arguments("ref var x = ref e.Current;", false)]
    [Arguments("e.Current = 1;", false)]
    [Arguments("(e.Current, x) = pair;", false)]
    [Arguments("((e.Current, x), y) = pair;", false)]
    [Arguments("var pair = (e.Current, x);", true)]
    [Arguments("pair = (e.Current, x);", true)]
    [Arguments("Use(e);", false)]
    [Arguments("Use(other.e);", false)]
    [Arguments("Use(e.Other);", false)]
    [Arguments("Use(e.Current<int>);", false)]
    [Arguments("Use(e->Current);", false)]
    [Arguments("var e = 1;", false)]
    [Arguments("System.Action<int> f = e => { };", false)]
    [Arguments("System.Action<int> f = other => { };", true)]
    [Arguments("foreach (var e in values) { }", false)]
    [Arguments("foreach (var other in values) { }", true)]
    [Arguments("try { } catch (System.Exception e) { }", false)]
    [Arguments("try { } catch (System.Exception other) { }", true)]
    [Arguments("try { } catch (System.Exception) { }", true)]
    [Arguments("if (item is int e) { }", false)]
    [Arguments("if (item is int other) { }", true)]
    [Arguments("void e() { }", false)]
    [Arguments("void Other() { }", true)]
    public async Task BodyMustOnlyReadCurrentAsync(string body, bool expected)
    {
        var loop = (WhileStatementSyntax)SyntaxFactory.ParseStatement($"while (e.MoveNext()) {{ {body} }}");
        await Assert.That(Sst1467UseForeachOverManualEnumeratorAnalyzer.HasForeachCompatibleBody(loop, "e")).IsEqualTo(expected);
    }

    /// <summary>Verifies a loop without a containing statement list cannot safely consume its declaration.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedLoopCannotBeRewrittenAsync()
    {
        var loop = (WhileStatementSyntax)SyntaxFactory.ParseStatement("while (e.MoveNext()) { }");
        await Assert.That(Sst1467UseForeachOverManualEnumeratorAnalyzer.TryGetEnumeratorDeclaration(loop, "e", out _, out _)).IsFalse();
        await Assert.That(Sst1467UseForeachOverManualEnumeratorAnalyzer.IsEnumeratorUsedAfterLoop(loop, "e")).IsTrue();
    }

    /// <summary>Verifies switch sections supply the declaration and later-use scope.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SwitchSectionLoopIsReportedAsync() =>
        VerifyForeach.VerifyAnalyzerAsync("""
            using System.Collections.Generic;
            class C
            {
                void M(List<int> values, int choice)
                {
                    switch (choice)
                    {
                        case 0:
                            var e = values.GetEnumerator();
                            {|SST1467:while|} (e.MoveNext()) { System.Console.Write(e.Current); }
                            break;
                    }
                }
            }
            """);

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
