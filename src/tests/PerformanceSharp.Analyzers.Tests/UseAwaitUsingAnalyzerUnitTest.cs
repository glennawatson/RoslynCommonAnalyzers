// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1310UseAwaitUsingAnalyzer,
    PerformanceSharp.Analyzers.Psh1310UseAwaitUsingCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1310UseAwaitUsingAnalyzer"/> (PSH1310 synchronous dispose in async code).</summary>
public class UseAwaitUsingAnalyzerUnitTest
{
    /// <summary>Verifies a synchronous using declaration in an async method is flagged and rewritten to await using.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingDeclarationInAsyncMethodIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading.Tasks;

                              public sealed class AsyncResource : IDisposable, IAsyncDisposable
                              {
                                  public void Dispose()
                                  {
                                  }

                                  public ValueTask DisposeAsync() => default;
                              }

                              public class C
                              {
                                  public async Task M()
                                  {
                                      {|PSH1310:using|} var resource = new AsyncResource();
                                      await Task.Yield();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Threading.Tasks;

                                   public sealed class AsyncResource : IDisposable, IAsyncDisposable
                                   {
                                       public void Dispose()
                                       {
                                       }

                                       public ValueTask DisposeAsync() => default;
                                   }

                                   public class C
                                   {
                                       public async Task M()
                                       {
                                           await using var resource = new AsyncResource();
                                           await Task.Yield();
                                       }
                                   }
                                   """;
        await VerifyNet90CodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a synchronous using statement in an async method is flagged and rewritten to await using.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingStatementInAsyncMethodIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading.Tasks;

                              public sealed class AsyncResource : IDisposable, IAsyncDisposable
                              {
                                  public void Dispose()
                                  {
                                  }

                                  public ValueTask DisposeAsync() => default;
                              }

                              public class C
                              {
                                  public async Task M()
                                  {
                                      {|PSH1310:using|} (var resource = new AsyncResource())
                                      {
                                          await Task.Yield();
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Threading.Tasks;

                                   public sealed class AsyncResource : IDisposable, IAsyncDisposable
                                   {
                                       public void Dispose()
                                       {
                                       }

                                       public ValueTask DisposeAsync() => default;
                                   }

                                   public class C
                                   {
                                       public async Task M()
                                       {
                                           await using (var resource = new AsyncResource())
                                           {
                                               await Task.Yield();
                                           }
                                       }
                                   }
                                   """;
        await VerifyNet90CodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a using statement over an existing expression whose type implements both interfaces is flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingStatementExpressionWithBothInterfacesIsFlaggedAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class AsyncResource : IDisposable, IAsyncDisposable
            {
                public void Dispose()
                {
                }

                public ValueTask DisposeAsync() => default;
            }

            public class C
            {
                public async Task M(AsyncResource resource)
                {
                    {|PSH1310:using|} (resource)
                    {
                        await Task.Yield();
                    }
                }
            }
            """);

    /// <summary>Verifies two flagged using declarations are both rewritten by the batched Fix All.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoUsingDeclarationsAreBothFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading.Tasks;

                              public sealed class AsyncResource : IDisposable, IAsyncDisposable
                              {
                                  public void Dispose()
                                  {
                                  }

                                  public ValueTask DisposeAsync() => default;
                              }

                              public class C
                              {
                                  public async Task M()
                                  {
                                      {|PSH1310:using|} var first = new AsyncResource();
                                      {|PSH1310:using|} var second = new AsyncResource();
                                      await Task.Yield();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Threading.Tasks;

                                   public sealed class AsyncResource : IDisposable, IAsyncDisposable
                                   {
                                       public void Dispose()
                                       {
                                       }

                                       public ValueTask DisposeAsync() => default;
                                   }

                                   public class C
                                   {
                                       public async Task M()
                                       {
                                           await using var first = new AsyncResource();
                                           await using var second = new AsyncResource();
                                           await Task.Yield();
                                       }
                                   }
                                   """;
        await VerifyNet90CodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a using declaration in a synchronous method stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingDeclarationInSynchronousMethodIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class AsyncResource : IDisposable, IAsyncDisposable
            {
                public void Dispose()
                {
                }

                public ValueTask DisposeAsync() => default;
            }

            public class C
            {
                public void M()
                {
                    using var resource = new AsyncResource();
                }
            }
            """);

    /// <summary>Verifies a resource that only implements IDisposable stays clean in an async method.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OnlyDisposableResourceIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class SyncResource : IDisposable
            {
                public void Dispose()
                {
                }
            }

            public class C
            {
                public async Task M()
                {
                    using var resource = new SyncResource();
                    await Task.Yield();
                }
            }
            """);

    /// <summary>Verifies an existing await using declaration stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitUsingDeclarationIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class AsyncResource : IDisposable, IAsyncDisposable
            {
                public void Dispose()
                {
                }

                public ValueTask DisposeAsync() => default;
            }

            public class C
            {
                public async Task M()
                {
                    await using var resource = new AsyncResource();
                    await Task.Yield();
                }
            }
            """);

    /// <summary>Verifies a using declaration in a synchronous lambda inside an async method stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingInSynchronousLambdaIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class AsyncResource : IDisposable, IAsyncDisposable
            {
                public void Dispose()
                {
                }

                public ValueTask DisposeAsync() => default;
            }

            public class C
            {
                public async Task M()
                {
                    Action dispose = () =>
                    {
                        using var resource = new AsyncResource();
                    };
                    dispose();
                    await Task.Yield();
                }
            }
            """);

    /// <summary>Verifies a multi-resource using statement stays clean when one declarator is not asynchronously disposable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedMultiDeclaratorUsingStatementIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class AsyncResource : IDisposable, IAsyncDisposable
            {
                public void Dispose()
                {
                }

                public ValueTask DisposeAsync() => default;
            }

            public sealed class SyncResource : IDisposable
            {
                public void Dispose()
                {
                }
            }

            public class C
            {
                public async Task M()
                {
                    using (IDisposable first = new AsyncResource(), second = new SyncResource())
                    {
                        await Task.Yield();
                    }
                }
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected source after the fix is applied.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90CodeFixAsync(string source, string fixedSource)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource,
        };
        await test.RunAsync(CancellationToken.None);
    }
}
