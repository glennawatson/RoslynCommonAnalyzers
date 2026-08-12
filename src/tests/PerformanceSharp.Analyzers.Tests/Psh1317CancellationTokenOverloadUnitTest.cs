// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyToken = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1317PassCancellationTokenAnalyzer,
    PerformanceSharp.Analyzers.Psh1317PassCancellationTokenCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for which method PSH1317 will hand a cancellation token to, and which slot it goes in.</summary>
public class Psh1317CancellationTokenOverloadUnitTest
{
    /// <summary>Verifies an optional token parameter left at its default is filled in.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionalTokenParameterIsFilledInAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send(int value, CancellationToken token = default)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      {|PSH1317:target.Send(1)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;

                                   public class Target
                                   {
                                       public void Send(int value, CancellationToken token = default)
                                       {
                                       }
                                   }

                                   public class C
                                   {
                                       public void M(Target target, CancellationToken cancellationToken)
                                       {
                                           target.Send(1, cancellationToken);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a token slot reached only past a skipped optional parameter is named.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SkippedOptionalParameterMakesTheTokenNamedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send(int value, int retries = 0, CancellationToken token = default)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      {|PSH1317:target.Send(1)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;

                                   public class Target
                                   {
                                       public void Send(int value, int retries = 0, CancellationToken token = default)
                                       {
                                       }
                                   }

                                   public class C
                                   {
                                       public void M(Target target, CancellationToken cancellationToken)
                                       {
                                           target.Send(1, token: cancellationToken);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a token parameter in the middle of an overload takes its own position.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TokenInTheMiddleOfAnOverloadTakesItsPositionAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send(int value, string label)
                                  {
                                  }

                                  public void Send(int value, CancellationToken token, string label)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      {|PSH1317:target.Send(1, "a")|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;

                                   public class Target
                                   {
                                       public void Send(int value, string label)
                                       {
                                       }

                                       public void Send(int value, CancellationToken token, string label)
                                       {
                                       }
                                   }

                                   public class C
                                   {
                                       public void M(Target target, CancellationToken cancellationToken)
                                       {
                                           target.Send(1, cancellationToken, "a");
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a call that already names its arguments is given a named token.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedArgumentsMakeTheTokenNamedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send(int value)
                                  {
                                  }

                                  public void Send(int value, CancellationToken token)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      {|PSH1317:target.Send(value: 1)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;

                                   public class Target
                                   {
                                       public void Send(int value)
                                       {
                                       }

                                       public void Send(int value, CancellationToken token)
                                       {
                                       }
                                   }

                                   public class C
                                   {
                                       public void M(Target target, CancellationToken cancellationToken)
                                       {
                                           target.Send(value: 1, token: cancellationToken);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an extension method's optional token parameter is filled in from the call site.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExtensionMethodOptionalTokenIsFilledInAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public static class QueryExtensions
                              {
                                  public static Task<List<T>> ToListAsync<T>(this IEnumerable<T> source, CancellationToken cancellationToken = default)
                                      => Task.FromResult(new List<T>());
                              }

                              public class C
                              {
                                  public async Task M(IEnumerable<int> source, CancellationToken cancellationToken)
                                  {
                                      _ = await {|PSH1317:source.ToListAsync()|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Threading;
                                   using System.Threading.Tasks;

                                   public static class QueryExtensions
                                   {
                                       public static Task<List<T>> ToListAsync<T>(this IEnumerable<T> source, CancellationToken cancellationToken = default)
                                           => Task.FromResult(new List<T>());
                                   }

                                   public class C
                                   {
                                       public async Task M(IEnumerable<int> source, CancellationToken cancellationToken)
                                       {
                                           _ = await source.ToListAsync(cancellationToken);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an overload that adds a token and an optional parameter is still a substitute.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverloadAddingAnOptionalParameterIsSuggestedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send(int value)
                                  {
                                  }

                                  public void Send(int value, CancellationToken token, string label = "")
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      {|PSH1317:target.Send(1)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;

                                   public class Target
                                   {
                                       public void Send(int value)
                                       {
                                       }

                                       public void Send(int value, CancellationToken token, string label = "")
                                       {
                                       }
                                   }

                                   public class C
                                   {
                                       public void M(Target target, CancellationToken cancellationToken)
                                       {
                                           target.Send(1, cancellationToken);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a call with no cancellable overload at all is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CallWithoutACancellableOverloadIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send(int value)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      target.Send(1);
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies an overload the call site cannot reach is not suggested.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InaccessibleOverloadIsNotSuggestedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send(int value)
                                  {
                                  }

                                  private void Send(int value, CancellationToken token)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      target.Send(1);
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies an overload that returns something else is not a substitute.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverloadWithADifferentReturnTypeIsNotSuggestedAsync()
    {
        const string Source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class Target
                              {
                                  public int Read() => 0;

                                  public Task<int> Read(CancellationToken token) => Task.FromResult(0);
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      _ = target.Read();
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies an overload that demands more than a token is not suggested.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverloadDemandingAnotherArgumentIsNotSuggestedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send(int value)
                                  {
                                  }

                                  public void Send(int value, string label, CancellationToken token)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      target.Send(1);
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies an overload that drops arguments the call already passes is not suggested.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverloadDroppingExistingArgumentsIsNotSuggestedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send(int value, string label)
                                  {
                                  }

                                  public void Send(CancellationToken token)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      target.Send(1, "a");
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies an overload that asks for two tokens is not suggested.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverloadWithTwoTokensIsNotSuggestedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send(int value)
                                  {
                                  }

                                  public void Send(int value, CancellationToken first, CancellationToken second)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      target.Send(1);
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies an overload whose parameter stops being a parameter array is not suggested.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverloadDroppingAParameterArrayIsNotSuggestedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Log(params string[] values)
                                  {
                                  }

                                  public void Log(string[] values, CancellationToken token)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      target.Log("a");
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a static sibling is not offered as an overload of an instance call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticSiblingIsNotSuggestedForAnInstanceCallAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send(int value)
                                  {
                                  }

                                  public static void Send(int value, CancellationToken token)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      target.Send(1);
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a generic call, whose type arguments do not carry across to a sibling, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericOverloadIsNotSuggestedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send<T>(T value)
                                  {
                                  }

                                  public void Send<T>(T value, CancellationToken token)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      target.Send(1);
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a separate extension overload, whose receiver is written before the dot, is not suggested.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparateExtensionOverloadIsNotSuggestedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                              }

                              public static class TargetExtensions
                              {
                                  public static void Send(this Target target, int value)
                                  {
                                  }

                                  public static void Send(this Target target, int value, CancellationToken token)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      target.Send(1);
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a required token parameter, which the call must already fill, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RequiredTokenParameterIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send(int value, CancellationToken token)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      target.Send(1, CancellationToken.None);
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a token supplied by name is seen even when earlier optional parameters are skipped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TokenSuppliedByNameIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class Target
                              {
                                  public void Send(int value = 0, CancellationToken token = default)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(Target target, CancellationToken cancellationToken)
                                  {
                                      target.Send(token: cancellationToken);
                                  }
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
