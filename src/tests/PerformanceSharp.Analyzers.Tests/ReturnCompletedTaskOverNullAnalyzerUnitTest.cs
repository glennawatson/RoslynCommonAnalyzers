// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1312ReturnCompletedTaskOverNullAnalyzer,
    PerformanceSharp.Analyzers.Psh1312ReturnCompletedTaskOverNullCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1312ReturnCompletedTaskOverNullAnalyzer"/> (PSH1312 completed task over null).</summary>
public class ReturnCompletedTaskOverNullAnalyzerUnitTest
{
    /// <summary>Verifies a null returned from a Task method is flagged and rewritten to the completed task.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullReturnInTaskMethodIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task M()
                                  {
                                      return {|PSH1312:null|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M()
                                       {
                                           return Task.CompletedTask;
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an expression-bodied Task method returning null is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedNullTaskIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task M() => {|PSH1312:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M() => Task.CompletedTask;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a default literal returned from a Task method is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultReturnInTaskMethodIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task M()
                                  {
                                      return {|PSH1312:default|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M()
                                       {
                                           return Task.CompletedTask;
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a null returned from a generic Task method is rewritten to a typed FromResult.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullReturnInGenericTaskMethodIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task<string> M()
                                  {
                                      return {|PSH1312:null|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task<string> M()
                                       {
                                           return Task.FromResult<string>(default);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a null returned from a Task-returning local function is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullReturnInLocalFunctionIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task M()
                                  {
                                      return Local();

                                      Task Local()
                                      {
                                          return {|PSH1312:null|};
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M()
                                       {
                                           return Local();

                                           Task Local()
                                           {
                                               return Task.CompletedTask;
                                           }
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an expression-bodied Task property returning null is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedNullTaskPropertyIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task Pending => {|PSH1312:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task Pending => Task.CompletedTask;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an async method returning a null result stays clean; that is a completed task carrying null.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncMethodReturningNullResultIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task<string> M()
                {
                    await Task.Yield();
                    return null;
                }
            }
            """);

    /// <summary>Verifies a default returned from a ValueTask method stays clean; that is already a completed task.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueTaskDefaultReturnIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public ValueTask M()
                {
                    return default;
                }
            }
            """);

    /// <summary>Verifies a null returned from a string method stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringMethodReturningNullIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string M()
                {
                    return null;
                }
            }
            """);

    /// <summary>Verifies a Task method already returning the completed task stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompletedTaskReturnIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public Task M()
                {
                    return Task.CompletedTask;
                }
            }
            """);

    /// <summary>Verifies that below C# 7.1 the generic fix emits an explicit default(T) instead of the bare default literal that version cannot parse.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericFixEmitsExplicitDefaultBelowCSharp71Async()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task<string> M() { return {|PSH1312:null|}; }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task<string> M() { return Task.FromResult<string>(default(string)); }
                                   }
                                   """;

        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = Source, FixedCode = FixedSource };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp7));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
