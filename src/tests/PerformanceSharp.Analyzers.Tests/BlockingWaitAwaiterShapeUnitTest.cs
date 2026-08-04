// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyBlocking = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1315NoBlockingWaitAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>
/// Covers the awaiter shapes PSH1315 recognizes beyond the chained
/// <c>task.GetAwaiter().GetResult()</c>: an awaiter parked in a local, and one reached through a
/// member. Both park a thread exactly as hard as the chained form.
/// </summary>
public class BlockingWaitAwaiterShapeUnitTest
{
    /// <summary>Verifies an awaiter parked in a local before <c>GetResult</c> is reported like the chained form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaiterHeldInALocalIsReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public sealed class C
                              {
                                  public void M(Task<int> task)
                                  {
                                      var awaiter = task.GetAwaiter();
                                      var value = {|PSH1315:awaiter.GetResult()|};
                                  }
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies the split shape is reported with a ConfigureAwait in front of the awaiter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfiguredAwaiterHeldInALocalIsReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public sealed class C
                              {
                                  public void M(Task task)
                                  {
                                      var awaiter = task.ConfigureAwait(false).GetAwaiter();
                                      {|PSH1315:awaiter.GetResult()|};
                                  }
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a completion check on the task still silences the split shape.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Following the local back to its initializer recovers the task, so the guard has a task to
    /// reason about rather than an awaiter it knows nothing about.
    /// </remarks>
    [Test]
    public async Task GuardedAwaiterHeldInALocalIsCleanAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public sealed class C
                              {
                                  public int M(Task<int> task)
                                  {
                                      if (task.IsCompletedSuccessfully)
                                      {
                                          var awaiter = task.GetAwaiter();
                                          return awaiter.GetResult();
                                      }

                                      return 0;
                                  }
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies an awaiter reached through a member is reported on its type alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaiterReachedThroughAMemberIsReportedAsync()
    {
        const string Source = """
                              using System.Runtime.CompilerServices;
                              using System.Threading.Tasks;

                              public sealed class C
                              {
                                  private TaskAwaiter<int> _awaiter;

                                  public int FromField() => {|PSH1315:_awaiter.GetResult()|};

                                  public void FromParameter(TaskAwaiter awaiter) => {|PSH1315:awaiter.GetResult()|};

                                  public int FromProperty() => {|PSH1315:Ready.GetResult()|};

                                  private TaskAwaiter<int> Ready { get; set; }
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a custom awaitable's own awaiter is not mistaken for a blocking wait.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomAwaiterIsCleanAsync()
    {
        const string Source = """
                              using System;
                              using System.Runtime.CompilerServices;

                              public readonly struct SignalAwaiter : INotifyCompletion
                              {
                                  public bool IsCompleted => true;

                                  public void OnCompleted(Action continuation) => continuation();

                                  public int GetResult() => 1;
                              }

                              public sealed class C
                              {
                                  public int M(SignalAwaiter awaiter) => awaiter.GetResult();
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source, with diagnostic markup where a diagnostic is expected.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAnalyzerAsync(string source)
    {
        var test = new VerifyBlocking.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
