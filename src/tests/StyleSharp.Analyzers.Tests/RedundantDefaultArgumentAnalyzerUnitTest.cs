// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRedundantDefault = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1494RedundantDefaultArgumentAnalyzer,
    StyleSharp.Analyzers.Sst1494RedundantDefaultArgumentCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1494 (an argument repeats the parameter's default) and its fix.</summary>
public class RedundantDefaultArgumentAnalyzerUnitTest
{
    /// <summary>Verifies a trailing argument that repeats the default is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingDefaultArgumentIsRemovedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void Run(int retries, bool verbose = false) => Run(retries, {|SST1494:false|});
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void Run(int retries, bool verbose = false) => Run(retries);
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a whole trailing run is reported, and fixing one of them drops the rest with it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingRunIsRemovedTogetherAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void Configure(string name, int size = 8, bool cache = true)
                                  {
                                  }

                                  public void Use() => Configure("a", {|SST1494:8|}, {|SST1494:true|});
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void Configure(string name, int size = 8, bool cache = true)
                                       {
                                       }

                                       public void Use() => Configure("a");
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an argument that is not last stops the walk, even when it matches its default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Dropping it would silently re-bind the argument that follows it, so it is not reported at all.</remarks>
    [Test]
    public async Task NonTrailingDefaultArgumentIsCleanAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Configure(int size = 8, bool cache = true)
                {
                }

                public void Use() => Configure(8, false);
            }
            """);

    /// <summary>Verifies a named trailing argument is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedTrailingArgumentIsRemovedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void Configure(int size = 8, bool cache = true)
                                  {
                                  }

                                  public void Use() => Configure({|SST1494:cache: true|});
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void Configure(int size = 8, bool cache = true)
                                       {
                                       }

                                       public void Use() => Configure();
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the value is compared as a constant, not as the text that spells it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantsAreComparedByValueAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private const int Zero = 0;

                public void Skip(int count = 0)
                {
                }

                public void Widen(long count = 0)
                {
                }

                public void Use()
                {
                    Skip({|SST1494:0x0|});
                    Skip({|SST1494:Zero|});
                    Widen({|SST1494:0|});
                }
            }
            """);

    /// <summary>Verifies an argument that differs from the default is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentValueIsCleanAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Skip(int count = 0, string label = "none")
                {
                }

                public void Use()
                {
                    Skip(1);
                    Skip(0, "all");
                }
            }
            """);

    /// <summary>Verifies an enum default and a null default are both recognized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumAndNullDefaultsAreRecognizedAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void Compare(StringComparison comparison = StringComparison.Ordinal)
                {
                }

                public void Name(string label = null)
                {
                }

                public void Use()
                {
                    Compare({|SST1494:StringComparison.Ordinal|});
                    Compare(StringComparison.OrdinalIgnoreCase);
                    Name({|SST1494:null|});
                }
            }
            """);

    /// <summary>Verifies an object creation and a constructor initializer are both call sites.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreationsAndInitializersAreCallSitesAsync()
    {
        const string Source = """
                              public class Base
                              {
                                  public Base(int size = 4)
                                  {
                                  }
                              }

                              public sealed class Derived : Base
                              {
                                  public Derived()
                                      : base({|SST1494:4|})
                                  {
                                      var other = new Base({|SST1494:4|});
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class Base
                                   {
                                       public Base(int size = 4)
                                       {
                                       }
                                   }

                                   public sealed class Derived : Base
                                   {
                                       public Derived()
                                           : base()
                                       {
                                           var other = new Base();
                                       }
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a caller-info argument is left to the rule that owns it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>SST1448 reports the same argument with the reason that actually applies, so this rule stays quiet.</remarks>
    [Test]
    public async Task CallerInfoArgumentIsNotReportedAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public sealed class C
            {
                public void Log(string message, [CallerMemberName] string member = "")
                {
                }

                public void Use() => Log("x", "");
            }
            """);

    /// <summary>Verifies a call inside an expression tree is left alone, because the omission would not compile.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionTreeCallIsCleanAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            using System;
            using System.Linq.Expressions;

            public sealed class C
            {
                public int Size(int size = 8) => size;

                public void Use()
                {
                    Expression<Func<C, int>> tree = c => c.Size(8);
                    Func<C, int> lambda = c => c.Size({|SST1494:8|});
                }
            }
            """);

    /// <summary>Verifies an argument whose omission would move the call to another overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The shortened call is bound before anything is reported. Here it would reach a different method, so
    /// the argument is not redundant at all — it is what selects the overload.
    /// </remarks>
    [Test]
    public async Task ArgumentThatSelectsTheOverloadIsCleanAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Send(int size) => size;

                public int Send(int size, bool cache = false) => size;

                public int Use() => Send(1, false);
            }
            """);

    /// <summary>Verifies an argument bound to a params parameter is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParamsArgumentIsCleanAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Send(int size = 0, params int[] values)
                {
                }

                public void Use() => Send(0, 0);
            }
            """);

    /// <summary>Verifies a trailing default argument on a conditional-access call is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Shortening the call means detaching and rebinding it, which orphans the conditional-access binding
    /// and crashes the binder, so the rule stays silent on the <c>receiver?.M(...)</c> form.
    /// </remarks>
    [Test]
    public async Task ConditionalAccessTrailingDefaultIsLeftAloneAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int a, int b = 0)
                {
                }

                public void Use(C c)
                {
                    c?.M(1, 0);
                }
            }
            """);
}
