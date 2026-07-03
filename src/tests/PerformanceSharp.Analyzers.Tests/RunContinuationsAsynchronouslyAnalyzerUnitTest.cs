// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1302RunContinuationsAsynchronouslyAnalyzer,
    PerformanceSharp.Analyzers.Psh1302RunContinuationsAsynchronouslyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1302RunContinuationsAsynchronouslyAnalyzer"/> (PSH1302 TaskCompletionSource continuations).</summary>
public class RunContinuationsAsynchronouslyAnalyzerUnitTest
{
    /// <summary>Verifies a bare completion source is flagged and the fix appends the flag.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareCompletionSourceIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task<int> M()
                                  {
                                      var tcs = {|PSH1302:new TaskCompletionSource<int>()|};
                                      return tcs.Task;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task<int> M()
                                       {
                                           var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                                           return tcs.Task;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a completion source that already passes the flag is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FlaggedCompletionSourceIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Threading.Tasks;

            public class C
            {
                public Task<int> M()
                {
                    var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                    return tcs.Task;
                }
            }
            """);

    /// <summary>Verifies a None options constant is flagged and substituted by the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoneOptionsAreFlaggedAndSubstitutedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task<int> M()
                                  {
                                      var tcs = {|PSH1302:new TaskCompletionSource<int>(TaskCreationOptions.None)|};
                                      return tcs.Task;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task<int> M()
                                       {
                                           var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                                           return tcs.Task;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies another option is flagged and or-combined by the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherOptionsAreFlaggedAndCombinedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task<int> M()
                                  {
                                      var tcs = {|PSH1302:new TaskCompletionSource<int>(TaskCreationOptions.AttachedToParent)|};
                                      return tcs.Task;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task<int> M()
                                       {
                                           var tcs = new TaskCompletionSource<int>(TaskCreationOptions.AttachedToParent | TaskCreationOptions.RunContinuationsAsynchronously);
                                           return tcs.Task;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the state-only constructor is flagged and the fix appends the options argument.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StateOnlyConstructorGainsOptionsArgumentAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task<int> M(object state)
                                  {
                                      var tcs = {|PSH1302:new TaskCompletionSource<int>(state)|};
                                      return tcs.Task;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task<int> M(object state)
                                       {
                                           var tcs = new TaskCompletionSource<int>(state, TaskCreationOptions.RunContinuationsAsynchronously);
                                           return tcs.Task;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a non-constant options argument stays clean because the flag may arrive at runtime.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OpaqueOptionsVariableIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Threading.Tasks;

            public class C
            {
                public Task<int> M(TaskCreationOptions options)
                {
                    var tcs = new TaskCompletionSource<int>(options);
                    return tcs.Task;
                }
            }
            """);

    /// <summary>Verifies an implicit creation is flagged and fixed in place.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitCreationIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  private readonly TaskCompletionSource<bool> _tcs = {|PSH1302:new()|};

                                  public Task<bool> Task => _tcs.Task;
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

                                       public Task<bool> Task => _tcs.Task;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the non-generic completion source is flagged too.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonGenericCompletionSourceIsFlaggedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task M()
                                  {
                                      var tcs = {|PSH1302:new TaskCompletionSource()|};
                                      return tcs.Task;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M()
                                       {
                                           var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                                           return tcs.Task;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string? fixedSource = null)
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
