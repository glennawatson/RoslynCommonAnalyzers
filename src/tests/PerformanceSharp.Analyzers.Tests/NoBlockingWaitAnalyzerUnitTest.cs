// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using VerifyBlocking = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1315NoBlockingWaitAnalyzer>;
using VerifyBlockingFix = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1315NoBlockingWaitAnalyzer,
    PerformanceSharp.Analyzers.Psh1315NoBlockingWaitCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1315 (do not block on a task that may not be complete) and its code fix.</summary>
public class NoBlockingWaitAnalyzerUnitTest
{
    /// <summary>Verifies a blocking Result read in an async method is reported and awaited instead.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockingResultIsAwaitedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M()
                                  {
                                      return {|PSH1315:LoadAsync().Result|};
                                  }

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<int> M()
                                       {
                                           return await LoadAsync();
                                       }

                                       private static Task<int> LoadAsync() => Task.FromResult(1);
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blocking Wait() in an async method is reported and awaited instead.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockingWaitIsAwaitedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M()
                                  {
                                      {|PSH1315:RunAsync().Wait()|};
                                  }

                                  private static Task RunAsync() => Task.CompletedTask;
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M()
                                       {
                                           await RunAsync();
                                       }

                                       private static Task RunAsync() => Task.CompletedTask;
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blocking GetAwaiter().GetResult() chain is reported and awaited instead.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockingGetResultIsAwaitedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M()
                                  {
                                      return {|PSH1315:LoadAsync().GetAwaiter().GetResult()|};
                                  }

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<int> M()
                                       {
                                           return await LoadAsync();
                                       }

                                       private static Task<int> LoadAsync() => Task.FromResult(1);
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a ValueTask is covered, not just a Task.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockingValueTaskResultIsAwaitedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M()
                                  {
                                      return {|PSH1315:LoadAsync().Result|};
                                  }

                                  private static ValueTask<int> LoadAsync() => new(1);
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<int> M()
                                       {
                                           return await LoadAsync();
                                       }

                                       private static ValueTask<int> LoadAsync() => new(1);
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blocking wait on a non-generic ValueTask is covered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockingValueTaskGetResultIsAwaitedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M()
                                  {
                                      {|PSH1315:RunAsync().GetAwaiter().GetResult()|};
                                  }

                                  private static ValueTask RunAsync() => default;
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M()
                                       {
                                           await RunAsync();
                                       }

                                       private static ValueTask RunAsync() => default;
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a ConfigureAwait in front of the awaiter is carried into the rewrite rather than dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfiguredAwaiterKeepsItsConfigureAwaitAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M()
                                  {
                                      return {|PSH1315:LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult()|};
                                  }

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<int> M()
                                       {
                                           return await LoadAsync().ConfigureAwait(false);
                                       }

                                       private static Task<int> LoadAsync() => Task.FromResult(1);
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the awaited replacement is parenthesized when the surrounding expression binds tighter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitIsParenthesizedWhenChainedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M()
                                  {
                                      return {|PSH1315:LoadAsync().Result|}.Length;
                                  }

                                  private static Task<string> LoadAsync() => Task.FromResult("x");
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<int> M()
                                       {
                                           return (await LoadAsync()).Length;
                                       }

                                       private static Task<string> LoadAsync() => Task.FromResult("x");
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blocking wait in a synchronous method the author owns is reported, with no fix offered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockingInSyncMethodIsReportedWithoutFixAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public int M()
                                  {
                                      return {|PSH1315:LoadAsync().Result|};
                                  }

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
                              }
                              """;
        await VerifyNoFixOfferedAsync(Source);
    }

    /// <summary>Verifies Wait with a timeout is reported but never rewritten, because awaiting would drop the timeout.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WaitWithTimeoutIsReportedWithoutFixAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M()
                                  {
                                      await Task.Yield();
                                      {|PSH1315:RunAsync().Wait(TimeSpan.FromSeconds(1))|};
                                  }

                                  private static Task RunAsync() => Task.CompletedTask;
                              }
                              """;
        await VerifyNoFixOfferedAsync(Source);
    }

    /// <summary>Verifies a blocking wait inside a lock is reported but not rewritten, because C# forbids awaiting there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockingInsideLockIsReportedWithoutFixAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  private readonly object _gate = new();

                                  public async Task M()
                                  {
                                      await Task.Yield();
                                      lock (_gate)
                                      {
                                          {|PSH1315:RunAsync().Wait()|};
                                      }
                                  }

                                  private static Task RunAsync() => Task.CompletedTask;
                              }
                              """;
        await VerifyNoFixOfferedAsync(Source);
    }

    /// <summary>Verifies the guarded fast path — the whole point of IsCompletedSuccessfully — is silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedFastPathIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M()
                                  {
                                      var pending = RunAsync();
                                      if (pending.IsCompletedSuccessfully)
                                      {
                                          pending.GetAwaiter().GetResult();
                                          return;
                                      }

                                      await pending;
                                  }

                                  private static ValueTask RunAsync() => default;
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a Result read gated by an IsCompleted check is silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedResultIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M()
                                  {
                                      var pending = LoadAsync();
                                      if (pending.IsCompleted)
                                      {
                                          return pending.Result;
                                      }

                                      return await pending;
                                  }

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies an early-return guard that has already left the method when the task was not complete is silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EarlyReturnGuardIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public int M(Task<int> pending)
                                  {
                                      if (!pending.IsCompletedSuccessfully)
                                      {
                                          return 0;
                                      }

                                      return pending.Result;
                                  }
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a ternary gated on completion is silent in both arms.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedTernaryIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M(Task<int> pending)
                                      => pending.IsCompleted ? pending.Result : await pending;

                                  public async Task<int> N(Task<int> pending)
                                      => !pending.IsCompleted ? await pending : pending.Result;
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a completion check on the left of an and-chain guards the right of it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedAndChainIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public bool M(Task<int> pending, bool enabled)
                                      => enabled && pending.IsCompletedSuccessfully && pending.Result > 0;
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a task that has already been awaited outright is not reported when its result is then read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitedTaskIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M(Task<int> pending)
                                  {
                                      await pending;
                                      return pending.Result;
                                  }
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a completion check on one task does not license blocking on another.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardOnAnotherTaskIsStillReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M(Task<int> first, Task<int> second)
                                  {
                                      await Task.Yield();
                                      if (first.IsCompleted)
                                      {
                                          return {|PSH1315:second.Result|};
                                      }

                                      return 0;
                                  }
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a completion check on a freshly fetched task guards nothing, because each call returns a different task.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CheckOnAFreshlyFetchedTaskIsStillReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M()
                                  {
                                      await Task.Yield();
                                      if (LoadAsync().IsCompleted)
                                      {
                                          return {|PSH1315:LoadAsync().Result|};
                                      }

                                      return 0;
                                  }

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies an awaiter's own GetResult is silent: the awaiter pattern requires it to be synchronous.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaiterGetResultIsNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Runtime.CompilerServices;
                              using System.Threading.Tasks;

                              public sealed class Awaiter : ICriticalNotifyCompletion
                              {
                                  private readonly Task<int> _task = Task.FromResult(1);

                                  public bool IsCompleted => _task.IsCompleted;

                                  public int GetResult() => _task.GetAwaiter().GetResult();

                                  public void OnCompleted(Action continuation) => _task.GetAwaiter().OnCompleted(continuation);

                                  public void UnsafeOnCompleted(Action continuation) => _task.GetAwaiter().UnsafeOnCompleted(continuation);
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a synchronous Dispose is silent: IDisposable owns the signature, so the author cannot await there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposeIsNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading.Tasks;

                              public sealed class C : IDisposable
                              {
                                  private readonly Task _pending = Task.CompletedTask;

                                  public void Dispose() => _pending.Wait();
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a synchronous method implementing an interface member is silent: the interface owns the signature.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceImplementationIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public interface IHandler
                              {
                                  void Handle();
                              }

                              public sealed class C : IHandler
                              {
                                  public void Handle() => RunAsync().Wait();

                                  private static Task RunAsync() => Task.CompletedTask;
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies an explicit interface implementation is silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitInterfaceImplementationIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public interface IHandler
                              {
                                  int Handle();
                              }

                              public sealed class C : IHandler
                              {
                                  int IHandler.Handle() => LoadAsync().Result;

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies an override is silent: the base type owns the signature.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverrideIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public abstract class Base
                              {
                                  public abstract int Load();
                              }

                              public sealed class C : Base
                              {
                                  public override int Load() => LoadAsync().Result;

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a property getter that implements an interface property is silent, while one the author owns is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfacePropertyIsNotReportedButAnOwnedOneIsAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public interface IValues
                              {
                                  int Value { get; }
                              }

                              public sealed class C : IValues
                              {
                                  private readonly Task<int> _pending = Task.FromResult(1);

                                  public int Value => _pending.Result;

                                  public int Owned => {|PSH1315:_pending.Result|};
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies the entry point is silent: Main is the outermost frame and the sanctioned bridge from sync to async.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EntryPointIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public static class Program
                              {
                                  public static void Main() => RunAsync().GetAwaiter().GetResult();

                                  private static Task RunAsync() => Task.CompletedTask;
                              }
                              """;
        var test = new VerifyBlocking.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        test.TestState.OutputKind = OutputKind.ConsoleApplication;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a method named Main in a library — where nothing makes it an entry point — is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MainInALibraryIsStillReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public static class Program
                              {
                                  public static void Main() => {|PSH1315:RunAsync().GetAwaiter().GetResult()|};

                                  private static Task RunAsync() => Task.CompletedTask;
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies generated code is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneratedCodeIsNotReportedAsync()
    {
        const string Source = """
                              // <auto-generated/>
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public int M() => LoadAsync().Result;

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a Wait on something that is not a task — a SemaphoreSlim — is not mistaken for a blocking wait.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SemaphoreWaitIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class C
                              {
                                  private readonly SemaphoreSlim _gate = new(1);

                                  public void M() => _gate.Wait();
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a synchronous local function inside an async method is reported without a fix, since it cannot await.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyncLocalFunctionInsideAsyncMethodIsReportedWithoutFixAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M()
                                  {
                                      await Task.Yield();

                                      int Local() => {|PSH1315:LoadAsync().Result|};
                                      Local();
                                  }

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
                              }
                              """;
        await VerifyNoFixOfferedAsync(Source);
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

    /// <summary>Runs a code-fix verification that expects the reported code to be rewritten.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixAsync(string source, string fixedSource)
    {
        var test = new VerifyBlockingFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification that expects the diagnostic to stand with no fix offered.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNoFixOfferedAsync(string source)
    {
        var test = new VerifyBlockingFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
