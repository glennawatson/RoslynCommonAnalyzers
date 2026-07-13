// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySwappedArguments = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2400SwappedArgumentsAnalyzer,
    StyleSharp.Analyzers.Sst2400SwappedArgumentsCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2400 (arguments passed in an order the parameter names contradict) and its fix.</summary>
public class SwappedArgumentsAnalyzerUnitTest
{
    /// <summary>Verifies a two-argument transposition is reported and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TransposedArgumentsAreSwappedBackAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void Copy(string source, string target)
                                  {
                                  }

                                  public void M(string source, string target)
                                  {
                                      Copy({|SST2400:target|}, source);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void Copy(string source, string target)
                                       {
                                       }

                                       public void M(string source, string target)
                                       {
                                           Copy(source, target);
                                       }
                                   }
                                   """;
        await VerifySwappedArguments.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies only the transposed pair moves when the call has other arguments around it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OnlyTheTransposedPairMovesAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void Copy(int count, string source, string target)
                                  {
                                  }

                                  public void M(int count, string source, string target)
                                  {
                                      Copy(count, {|SST2400:target|}, source);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void Copy(int count, string source, string target)
                                       {
                                       }

                                       public void M(int count, string source, string target)
                                       {
                                           Copy(count, source, target);
                                       }
                                   }
                                   """;
        await VerifySwappedArguments.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a constructor's arguments are measured like any other call's.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorArgumentsAreReportedAsync()
    {
        const string Source = """
                              public sealed class Span
                              {
                                  public Span(int start, int end)
                                  {
                                      Start = start;
                                      End = end;
                                  }

                                  public int Start { get; }

                                  public int End { get; }
                              }

                              public sealed class C
                              {
                                  public Span Build(int start, int end) => new Span({|SST2400:end|}, start);
                              }
                              """;
        const string FixedSource = """
                                   public sealed class Span
                                   {
                                       public Span(int start, int end)
                                       {
                                           Start = start;
                                           End = end;
                                       }

                                       public int Start { get; }

                                       public int End { get; }
                                   }

                                   public sealed class C
                                   {
                                       public Span Build(int start, int end) => new Span(start, end);
                                   }
                                   """;
        await VerifySwappedArguments.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local named after a parameter is enough — the names are what is read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalsNamedAfterParametersAreReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void Move(string from, string to)
                                  {
                                  }

                                  public void M()
                                  {
                                      var from = "a";
                                      var to = "b";
                                      Move({|SST2400:to|}, from);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void Move(string from, string to)
                                       {
                                       }

                                       public void M()
                                       {
                                           var from = "a";
                                           var to = "b";
                                           Move(from, to);
                                       }
                                   }
                                   """;
        await VerifySwappedArguments.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies arguments already in the parameters' order are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentsInOrderAreCleanAsync()
        => await VerifySwappedArguments.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Copy(string source, string target)
                {
                }

                public void M(string source, string target) => Copy(source, target);
            }
            """);

    /// <summary>Verifies a named argument settles the order at the call site, so nothing is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedArgumentsAreCleanAsync()
        => await VerifySwappedArguments.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Copy(string source, string target)
                {
                }

                public void M(string source, string target) => Copy(target: target, source: source);
            }
            """);

    /// <summary>Verifies a rotation is not a transposition, and is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>There is no unambiguous repair for one, so reading it as a mistake would be a guess.</remarks>
    [Test]
    public async Task RotationIsNotReportedAsync()
        => await VerifySwappedArguments.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Take(int first, int second, int third)
                {
                }

                public void M(int first, int second, int third) => Take(second, third, first);
            }
            """);

    /// <summary>Verifies a pair whose parameters differ in type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Reordering would change which overload the call binds to, or stop it compiling at all.</remarks>
    [Test]
    public async Task DifferentParameterTypesAreCleanAsync()
        => await VerifySwappedArguments.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Write(object value, string name)
                {
                }

                public void M(object name, string value) => Write(name, value);
            }
            """);

    /// <summary>Verifies an argument that is not a bare identifier is not read as a name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComputedArgumentIsCleanAsync()
        => await VerifySwappedArguments.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Copy(string source, string target)
                {
                }

                public string Get(string value) => value;

                public void M(string source, string target) => Copy(Get(target), source);
            }
            """);

    /// <summary>Verifies a call with a <c>params</c> tail is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterArrayIsCleanAsync()
        => await VerifySwappedArguments.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Log(string message, params object[] arguments)
                {
                }

                public void M(string message, object[] arguments) => Log(message, arguments);
            }
            """);

    /// <summary>Verifies an argument naming a parameter it is already in the right place for is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentNamingItsOwnParameterIsCleanAsync()
        => await VerifySwappedArguments.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Copy(string source, string target)
                {
                }

                public void M(string source, string other) => Copy(source, other);
            }
            """);
}
