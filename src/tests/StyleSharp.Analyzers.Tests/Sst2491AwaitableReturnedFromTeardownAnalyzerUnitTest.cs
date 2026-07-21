// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;
using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2491AwaitableReturnedFromTeardownAnalyzer,
    StyleSharp.Analyzers.Sst2491AwaitableReturnedFromTeardownCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2491 (a pending task returned from inside a using, lock, or try/finally).</summary>
public class Sst2491AwaitableReturnedFromTeardownAnalyzerUnitTest
{
    /// <summary>Verifies a generic task returned from a using declaration is reported and made async.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericTaskInUsingDeclarationIsReportedAsync()
    {
        const string Source = """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public Task<int> Read()
                {
                    using var scope = new Scope();
                    {|SST2491:return scope.LoadAsync();|}
                }
            }

            public sealed class Scope : IDisposable
            {
                public void Dispose() { }
                public Task<int> LoadAsync() => Task.FromResult(0);
            }
            """;
        const string Fixed = """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task<int> Read()
                {
                    using var scope = new Scope();
                    return await scope.LoadAsync();
                }
            }

            public sealed class Scope : IDisposable
            {
                public void Dispose() { }
                public Task<int> LoadAsync() => Task.FromResult(0);
            }
            """;
        await VerifyFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a generic task returned from a using statement is reported and made async.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericTaskInUsingStatementIsReportedAsync()
    {
        const string Source = """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public Task<int> Read()
                {
                    using (var scope = new Scope())
                    {
                        {|SST2491:return scope.LoadAsync();|}
                    }
                }
            }

            public sealed class Scope : IDisposable
            {
                public void Dispose() { }
                public Task<int> LoadAsync() => Task.FromResult(0);
            }
            """;
        const string Fixed = """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task<int> Read()
                {
                    using (var scope = new Scope())
                    {
                        return await scope.LoadAsync();
                    }
                }
            }

            public sealed class Scope : IDisposable
            {
                public void Dispose() { }
                public Task<int> LoadAsync() => Task.FromResult(0);
            }
            """;
        await VerifyFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a task returned from a try/finally body is reported and made async.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TaskInTryFinallyIsReportedAsync()
    {
        const string Source = """
            using System.Threading.Tasks;

            public sealed class C
            {
                public Task<int> Read()
                {
                    try
                    {
                        {|SST2491:return LoadAsync();|}
                    }
                    finally
                    {
                    }
                }

                private Task<int> LoadAsync() => Task.FromResult(0);
            }
            """;
        const string Fixed = """
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task<int> Read()
                {
                    try
                    {
                        return await LoadAsync();
                    }
                    finally
                    {
                    }
                }

                private Task<int> LoadAsync() => Task.FromResult(0);
            }
            """;
        await VerifyFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a non-generic task returned from a using is reported and awaited before returning.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonGenericTaskInUsingIsReportedAsync()
    {
        const string Source = """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public Task Save()
                {
                    using var scope = new Scope();
                    {|SST2491:return scope.WriteAsync();|}
                }
            }

            public sealed class Scope : IDisposable
            {
                public void Dispose() { }
                public Task WriteAsync() => Task.CompletedTask;
            }
            """;
        const string Fixed = """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task Save()
                {
                    using var scope = new Scope();
                    {
                        await scope.WriteAsync();
                        return;
                    }
                }
            }

            public sealed class Scope : IDisposable
            {
                public void Dispose() { }
                public Task WriteAsync() => Task.CompletedTask;
            }
            """;
        await VerifyFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a task returned from inside a lock is reported but offered no fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TaskInLockIsReportedWithoutFixAsync()
    {
        const string Source = """
            using System.Threading.Tasks;

            public sealed class C
            {
                private readonly object _gate = new();

                public Task<int> Read()
                {
                    lock (_gate)
                    {
                        {|SST2491:return LoadAsync();|}
                    }
                }

                private Task<int> LoadAsync() => Task.FromResult(0);
            }
            """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies completed-task shapes returned from a using are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompletedTaskShapesAreCleanAsync()
    {
        const string Source = """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public Task<int> ReadResult()
                {
                    using var scope = new Scope();
                    return Task.FromResult(1);
                }

                public Task Complete()
                {
                    using var scope = new Scope();
                    return Task.CompletedTask;
                }

                public Task<int> MaybeNull()
                {
                    using var scope = new Scope();
                    return null;
                }
            }

            public sealed class Scope : IDisposable
            {
                public void Dispose() { }
            }
            """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies an already-async method and a return outside any teardown are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncMethodAndReturnOutsideTeardownAreCleanAsync()
    {
        const string Source = """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task<int> AlreadyAsync()
                {
                    using var scope = new Scope();
                    return await scope.LoadAsync();
                }

                public Task<int> NoTeardown()
                {
                    return LoadAsync();
                }

                private Task<int> LoadAsync() => Task.FromResult(0);
            }

            public sealed class Scope : IDisposable
            {
                public void Dispose() { }
                public Task<int> LoadAsync() => Task.FromResult(0);
            }
            """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Runs a code-fix verification against the .NET 8 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixAsync(string source, string fixedSource)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
            FixedCode = fixedSource,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 8 reference assemblies.</summary>
    /// <param name="source">The source with any diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAnalyzerAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
