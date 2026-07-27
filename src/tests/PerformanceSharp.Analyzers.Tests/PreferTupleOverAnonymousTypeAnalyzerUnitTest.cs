// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1023PreferTupleOverAnonymousTypeAnalyzer,
    PerformanceSharp.Analyzers.Psh1023PreferTupleOverAnonymousTypeCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1023 (a local anonymous type that could be a tuple).</summary>
public class PreferTupleOverAnonymousTypeAnalyzerUnitTest
{
    /// <summary>Verifies a member-read-only local is reported and rewritten as a tuple.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalReadThroughMembersRewrittenAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M()
                                  {
                                      var pair = {|PSH1023:new { Left = 1, Right = 2 }|};
                                      return pair.Left + pair.Right;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M()
                                       {
                                           var pair = (Left: 1, Right: 2);
                                           return pair.Left + pair.Right;
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an unnamed member takes the name its expression implies.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InferredMemberNamesRewrittenAsync()
    {
        const string Source = """
                              public sealed class Source
                              {
                                  public int Width { get; set; }

                                  public int Height { get; set; }
                              }

                              public sealed class C
                              {
                                  public int M(Source source)
                                  {
                                      var size = {|PSH1023:new { source.Width, source.Height }|};
                                      return size.Width * size.Height;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class Source
                                   {
                                       public int Width { get; set; }

                                       public int Height { get; set; }
                                   }

                                   public sealed class C
                                   {
                                       public int M(Source source)
                                       {
                                           var size = (Width: source.Width, Height: source.Height);
                                           return size.Width * size.Height;
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local that is returned is left alone, since its type is part of a contract.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnedLocalIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public object M()
                {
                    var pair = new { Left = 1, Right = 2 };
                    return pair;
                }
            }
            """);

    /// <summary>Verifies a local passed on as an argument is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PassedLocalIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Consume(object value)
                {
                }

                public void M()
                {
                    var pair = new { Left = 1, Right = 2 };
                    Consume(pair);
                }
            }
            """);

    /// <summary>Verifies a single-member anonymous type is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleMemberIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M()
                {
                    var wrapper = new { Only = 1 };
                    return wrapper.Only;
                }
            }
            """);

    /// <summary>Verifies an anonymous type that is not a local's initializer is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLocalAnonymousTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Consume(object value)
                {
                }

                public void M() => Consume(new { Left = 1, Right = 2 });
            }
            """);
}
