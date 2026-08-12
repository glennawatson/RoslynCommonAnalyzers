// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyToken = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1317PassCancellationTokenAnalyzer,
    PerformanceSharp.Analyzers.Psh1317PassCancellationTokenCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1317 (a call that drops a cancellation token the call site is holding) and its code fix.</summary>
public class Psh1317PassCancellationTokenAnalyzerUnitTest
{
    /// <summary>A target type whose token overload sits at the end of the parameter list.</summary>
    private const string TrailingTokenTarget = """
        public class Target
        {
            public void Send(int value)
            {
            }

            public void Send(int value, CancellationToken token)
            {
            }
        }
        """;

    /// <summary>Verifies an overload that takes a token is preferred over the one that drops it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverloadTakingTokenIsPassedTheTokenAsync()
    {
        const string Source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M(CancellationToken cancellationToken)
                                  {
                                      await {|PSH1317:Task.Delay(100)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M(CancellationToken cancellationToken)
                                       {
                                           await Task.Delay(100, cancellationToken);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a call with no arguments at all is given the token as its only one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyArgumentListTakesTheTokenAsync()
    {
        const string Source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M(SemaphoreSlim gate, CancellationToken cancellationToken)
                                  {
                                      await {|PSH1317:gate.WaitAsync()|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M(SemaphoreSlim gate, CancellationToken cancellationToken)
                                       {
                                           await gate.WaitAsync(cancellationToken);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the token is found through a qualified parameter type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedTokenTypeIsFoundAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M(System.Threading.CancellationToken cancellationToken)
                                  {
                                      await {|PSH1317:Task.Delay(100)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M(System.Threading.CancellationToken cancellationToken)
                                       {
                                           await Task.Delay(100, cancellationToken);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a lambda's own token parameter is passed to calls in its body.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LambdaTokenParameterIsPassedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public void M()
                                  {
                                      Register((CancellationToken token) => {|PSH1317:Task.Delay(100)|});
                                  }

                                  private static void Register(Func<CancellationToken, Task> callback)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Threading;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public void M()
                                       {
                                           Register((CancellationToken token) => Task.Delay(100, token));
                                       }

                                       private static void Register(Func<CancellationToken, Task> callback)
                                       {
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a static local function's own token parameter is passed, even though it captures nothing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticLocalFunctionTokenParameterIsPassedAsync()
    {
        const string Source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M()
                                  {
                                      await Run(CancellationToken.None);

                                      static async Task Run(CancellationToken token) => await {|PSH1317:Task.Delay(100)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M()
                                       {
                                           await Run(CancellationToken.None);

                                           static async Task Run(CancellationToken token) => await Task.Delay(100, token);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an anonymous method's token parameter is passed to calls in its body.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnonymousMethodTokenParameterIsPassedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public void M()
                                  {
                                      Register(delegate (CancellationToken token)
                                      {
                                          _ = {|PSH1317:Task.Delay(100)|};
                                      });
                                  }

                                  private static void Register(Action<CancellationToken> callback)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Threading;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public void M()
                                       {
                                           Register(delegate (CancellationToken token)
                                           {
                                               _ = Task.Delay(100, token);
                                           });
                                       }

                                       private static void Register(Action<CancellationToken> callback)
                                       {
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an implicitly typed lambda still passes the token its enclosing method holds.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitlyTypedLambdaPassesTheEnclosingTokenAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public void M(CancellationToken cancellationToken)
                                  {
                                      Register((delay, label) => {|PSH1317:Task.Delay(delay)|});
                                  }

                                  private static void Register(Func<int, string, Task> callback)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Threading;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public void M(CancellationToken cancellationToken)
                                       {
                                           Register((delay, label) => Task.Delay(delay, cancellationToken));
                                       }

                                       private static void Register(Func<int, string, Task> callback)
                                       {
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies repeated calls to one method are each reported, with the overload search paid for once.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RepeatedCallsToOneMethodAreEachReportedAsync()
    {
        const string Source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M(CancellationToken cancellationToken)
                                  {
                                      await {|PSH1317:Task.Delay(100)|};
                                      await {|PSH1317:Task.Delay(200)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M(CancellationToken cancellationToken)
                                       {
                                           await Task.Delay(100, cancellationToken);
                                           await Task.Delay(200, cancellationToken);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a call reached through a conditional access is reported but offered no fix — rebinding the detached call would orphan its member binding.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalAccessCallReportsWithoutOfferingAFixAsync()
    {
        var source = $$"""
                       using System.Threading;

                       {{TrailingTokenTarget}}

                       public class C
                       {
                           public void M(Target target, CancellationToken cancellationToken)
                           {
                               target?{|PSH1317:.Send(1)|};
                           }
                       }
                       """;
        await VerifyNet90Async(source, source);
    }

    /// <summary>Verifies a call that already passes the token is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CallThatAlreadyPassesTheTokenIsNotReportedAsync()
    {
        var source = $$"""
                       using System.Threading;

                       {{TrailingTokenTarget}}

                       public class C
                       {
                           public void M(Target target, CancellationToken cancellationToken)
                           {
                               target.Send(1, cancellationToken);
                           }
                       }
                       """;
        await VerifyNet90Async(source, source);
    }

    /// <summary>Verifies an explicit opt-out with the empty token is respected.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitEmptyTokenIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M(CancellationToken cancellationToken)
                                  {
                                      await Task.Delay(100, CancellationToken.None);
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a call site with no token in scope is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CallWithNoTokenInScopeIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M()
                                  {
                                      await Task.Delay(100);
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a member without a body of its own never looks outward for a token.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyBodyIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task P => Task.Delay(100);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a static lambda, which can capture nothing, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticLambdaIsNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public void M(CancellationToken cancellationToken)
                                  {
                                      Register(static () => Task.Delay(100));
                                  }

                                  private static void Register(Func<Task> callback)
                                  {
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a static single-parameter lambda, which can capture nothing, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticSingleParameterLambdaIsNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public void M(CancellationToken cancellationToken)
                                  {
                                      Register(static delay => Task.Delay(delay));
                                  }

                                  private static void Register(Func<int, Task> callback)
                                  {
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a static anonymous method, which can capture nothing, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticAnonymousMethodIsNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public void M(CancellationToken cancellationToken)
                                  {
                                      Register(static delegate
                                      {
                                          _ = Task.Delay(100);
                                      });
                                  }

                                  private static void Register(Action callback)
                                  {
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a parameter of a same-named type from another namespace is not mistaken for a token.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedTypeFromAnotherNamespaceIsNotATokenAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              namespace Custom
                              {
                                  public struct CancellationToken
                                  {
                                  }

                                  public class C
                                  {
                                      public async Task M(CancellationToken cancellationToken)
                                      {
                                          await Task.Delay(100);
                                      }
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a token that can only be written to is never passed on.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OutTokenParameterIsNotPassedAsync()
    {
        const string Source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public void M(out CancellationToken token)
                                  {
                                      token = default;
                                      _ = Task.Delay(100);
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a by-reference token, which a nested function could not capture, is never passed on.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InTokenParameterIsNotPassedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public void M(in CancellationToken token)
                                  {
                                      _ = Task.Delay(100);
                                      Register(() => Task.Delay(100));
                                  }

                                  private static void Register(Func<Task> callback)
                                  {
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies an invocation that binds to nothing is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NameofExpressionIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class C
                              {
                                  public string M(CancellationToken cancellationToken) => nameof(M);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyToken.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
