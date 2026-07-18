// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyMemberCopyDeconstruction = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2247MemberCopyDeconstructionAnalyzer,
    StyleSharp.Analyzers.Sst2247MemberCopyDeconstructionCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2247MemberCopyDeconstructionAnalyzer"/> and its code fix (SST2247).</summary>
public class MemberCopyDeconstructionAnalyzerUnitTest
{
    /// <summary>Verifies tuple member copies off a parameter fold into a deconstruction.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TupleMemberCopiesAreFoldedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M((int, int) pair)
                                  {
                                      {|SST2247:var a = pair.Item1;|}
                                      var b = pair.Item2;
                                      return a + b;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M((int, int) pair)
                                       {
                                           var (a, b) = pair;
                                           return a + b;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies named tuple element copies fold and preserve the original local names.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedTupleElementCopiesAreFoldedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M((string Name, int Age) person)
                                  {
                                      {|SST2247:var name = person.Name;|}
                                      var age = person.Age;
                                      return name + age;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M((string Name, int Age) person)
                                       {
                                           var (name, age) = person;
                                           return name + age;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a three-element tuple read in order folds into a deconstruction.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreeElementTupleCopiesAreFoldedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M((int, int, int) triple)
                                  {
                                      {|SST2247:var a = triple.Item1;|}
                                      var b = triple.Item2;
                                      var c = triple.Item3;
                                      return a + b + c;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M((int, int, int) triple)
                                       {
                                           var (a, b, c) = triple;
                                           return a + b + c;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies member copies off a value exposing a matching Deconstruct fold into a deconstruction.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeconstructableMemberCopiesAreFoldedAsync()
    {
        const string Source = """
                              public readonly struct Point
                              {
                                  public Point(int x, int y)
                                  {
                                      X = x;
                                      Y = y;
                                  }

                                  public int X { get; }

                                  public int Y { get; }

                                  public void Deconstruct(out int x, out int y)
                                  {
                                      x = X;
                                      y = Y;
                                  }
                              }

                              public sealed class C
                              {
                                  public int M(Point point)
                                  {
                                      {|SST2247:var x = point.X;|}
                                      var y = point.Y;
                                      return x + y;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public readonly struct Point
                                   {
                                       public Point(int x, int y)
                                       {
                                           X = x;
                                           Y = y;
                                       }

                                       public int X { get; }

                                       public int Y { get; }

                                       public void Deconstruct(out int x, out int y)
                                       {
                                           x = X;
                                           y = Y;
                                       }
                                   }

                                   public sealed class C
                                   {
                                       public int M(Point point)
                                       {
                                           var (x, y) = point;
                                           return x + y;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a single member read is left alone; there is nothing to deconstruct.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleMemberReadIsCleanAsync()
        => await RunAsync(
            """
            public sealed class C
            {
                public int M((int, int) pair)
                {
                    var a = pair.Item1;
                    return a;
                }
            }
            """);

    /// <summary>Verifies members read out of positional order are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReorderedMemberReadsAreCleanAsync()
        => await RunAsync(
            """
            public sealed class C
            {
                public int M((int, int) pair)
                {
                    var a = pair.Item2;
                    var b = pair.Item1;
                    return a + b;
                }
            }
            """);

    /// <summary>Verifies a partial read that omits a tuple position is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialTupleReadIsCleanAsync()
        => await RunAsync(
            """
            public sealed class C
            {
                public int M((int, int, int) triple)
                {
                    var a = triple.Item1;
                    var b = triple.Item2;
                    return a + b;
                }
            }
            """);

    /// <summary>Verifies member copies off a value with no matching deconstruction are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonDeconstructibleSourceIsCleanAsync()
        => await RunAsync(
            """
            public sealed class Box
            {
                public int First { get; set; }

                public int Second { get; set; }
            }

            public sealed class C
            {
                public int M(Box box)
                {
                    var a = box.First;
                    var b = box.Second;
                    return a + b;
                }
            }
            """);

    /// <summary>Verifies copies whose member names do not match the Deconstruct parameters are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeconstructNameMismatchIsCleanAsync()
        => await RunAsync(
            """
            public readonly struct Span
            {
                public Span(int start, int end)
                {
                    Start = start;
                    End = end;
                }

                public int Start { get; }

                public int End { get; }

                public void Deconstruct(out int from, out int to)
                {
                    from = Start;
                    to = End;
                }
            }

            public sealed class C
            {
                public int M(Span span)
                {
                    var a = span.Start;
                    var b = span.End;
                    return a + b;
                }
            }
            """);

    /// <summary>Verifies copies from two different sources are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentSourcesAreCleanAsync()
        => await RunAsync(
            """
            public sealed class C
            {
                public int M((int, int) first, (int, int) second)
                {
                    var a = first.Item1;
                    var b = second.Item2;
                    return a + b;
                }
            }
            """);

    /// <summary>Verifies explicitly typed member copies are left alone; folding could change the inferred types.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitlyTypedCopiesAreCleanAsync()
        => await RunAsync(
            """
            public sealed class C
            {
                public long M((int, int) pair)
                {
                    long a = pair.Item1;
                    long b = pair.Item2;
                    return a + b;
                }
            }
            """);

    /// <summary>Verifies a value declared in the immediately preceding statement is left to the tuple-temporary rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SourceDeclaredJustBeforeIsCleanAsync()
        => await RunAsync(
            """
            public sealed class C
            {
                public int M()
                {
                    var pair = (1, 2);
                    var a = pair.Item1;
                    var b = pair.Item2;
                    return a + b;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent below C# 7, where deconstruction declarations do not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp7Async()
    {
        const string Source = """
                              public struct Point
                              {
                                  public Point(int x, int y)
                                  {
                                      X = x;
                                      Y = y;
                                  }

                                  public int X { get; }

                                  public int Y { get; }

                                  public void Deconstruct(out int x, out int y)
                                  {
                                      x = X;
                                      y = Y;
                                  }
                              }

                              public sealed class C
                              {
                                  public int M(Point point)
                                  {
                                      var x = point.X;
                                      var y = point.Y;
                                      return x + y;
                                  }
                              }
                              """;
        var test = new VerifyMemberCopyDeconstruction.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp6));
        });
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the verifier against the .NET 8 reference pack with a modern C# parse option.</summary>
    /// <param name="source">The source to analyze.</param>
    /// <param name="fixedSource">The optional fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string? fixedSource = null)
    {
        var test = new VerifyMemberCopyDeconstruction.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source
        };

        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp12));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
