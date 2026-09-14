// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using Analyze = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2491AwaitableReturnedFromTeardownAnalyzer>;
using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2491AwaitableReturnedFromTeardownAnalyzer,
    StyleSharp.Analyzers.Sst2491AwaitableReturnedFromTeardownCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2491 (a pending task returned from inside a using, lock, or try/finally).</summary>
public class Sst2491AwaitableReturnedFromTeardownAnalyzerUnitTest
{
    /// <summary>Checks both value-task forms and nested parentheses retain the teardown diagnostic.</summary>
    /// <param name="returnType">The declared task type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Task")]
    [Arguments("Task<int>")]
    [Arguments("ValueTask")]
    [Arguments("ValueTask<int>")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PendingTaskTypesInLocalFunctionsAreReportedAsync(string returnType) =>
        Analyze.VerifyAnalyzerAsync($$"""
            using System.Threading.Tasks;
            class C
            {
                void Outer()
                {
                    {{returnType}} Read({{returnType}} pending)
                    {
                        lock (this) { {|SST2491:return ((pending));|} }
                    }
                }
            }
            """);

    /// <summary>Checks syntax recognized as already complete stays silent inside teardown scopes.</summary>
    /// <param name="expression">The completed task expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("((Task.CompletedTask))")]
    [Arguments("default")]
    [Arguments("default(Task)")]
    [Arguments("null")]
    [Arguments("Task.FromResult(1)")]
    [Arguments("Task.FromException(new System.Exception())")]
    [Arguments("Task.FromCanceled(new System.Threading.CancellationToken(true))")]
    [Arguments("new Task(() => {})")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CompletedSyntaxShapesAreCleanAsync(string expression) =>
        Analyze.VerifyAnalyzerAsync($$"""using System.Threading.Tasks; class C { Task M() { lock (this) { return {{expression}}; } } }""");

    /// <summary>Checks function boundaries, non-task returns, and nongoverning scopes stay silent.</summary>
    /// <param name="member">The declaration containing a return.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("void M() { lock (this) { return; } }")]
    [Arguments("int M() { lock (this) { return 1; } }")]
    [Arguments("int M(int value) { lock (this) { return value; } }")]
    [Arguments("T M<T>(T value) { lock (this) { return value; } }")]
    [Arguments("Task P { get { lock (this) { return pending; } } }")]
    [Arguments("System.Func<Task> M() => () => { lock (this) { return pending; } };")]
    [Arguments("System.Func<Task> M() => delegate { lock (this) { return pending; } };")]
    [Arguments("Task M() { lock (this) { Task Local() { return pending; } } return pending; }")]
    [Arguments("Task M() { try { return pending; } catch { return pending; } }")]
    [Arguments("Task M() { try { throw new System.Exception(); } catch { return pending; } finally {} }")]
    [Arguments("Task M() { return pending; using var scope = new System.IO.MemoryStream(); }")]
    [Arguments("Task M() { var value = 1; if (value > 0) return pending; return pending; }")]
    [Arguments("void M() { async Task<int> Local() { using var scope = new System.IO.MemoryStream(); return await Task.FromResult(1); } }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NongoverningReturnsAreCleanAsync(string member) =>
        Analyze.VerifyAnalyzerAsync($$"""using System.Threading.Tasks; class C { Task pending; {{member}} }""");

    /// <summary>Checks a pending task member is not mistaken for the completed-task property.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PendingTaskMemberIsReportedAsync() =>
        Analyze.VerifyAnalyzerAsync("""
            using System.Threading.Tasks;
            class C { Task pending; Task M() { lock (this) { {|SST2491:return this.pending;|} } } }
            """);

    /// <summary>Checks a top-level return has no enclosing supported function.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task TopLevelReturnIsCleanAsync() =>
        new Analyze.Test { TestCode = "lock (new object()) { return pending; }", CompilerDiagnostics = CompilerDiagnostics.None }.RunAsync(CancellationToken.None);

    /// <summary>Checks missing task metadata prevents reports after the syntax checks succeed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingTaskTypesAreCleanAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { MissingTask M() { lock (this) { return pending; } } }");
        var compilation = CSharpCompilation.Create(nameof(MissingTaskTypesAreCleanAsync), [tree]);
        var diagnostics = await compilation.WithAnalyzers([new Sst2491AwaitableReturnedFromTeardownAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Checks task availability handles value-task-only and entirely absent frameworks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TaskTypeAvailabilityRequiresANongenericTaskTypeAsync()
    {
        var compilation = CSharpCompilation.Create(nameof(TaskTypeAvailabilityRequiresANongenericTaskTypeAsync), references: RuntimeMetadataReferences.Platform);
        var resolved = Sst2491AwaitableReturnedFromTeardownAnalyzer.TeardownTaskTypes.Resolve(compilation);
        await Assert.That((resolved with { Task = null }).Any).IsTrue();
        await Assert.That((resolved with { Task = null, ValueTask = null }).Any).IsFalse();
        await Assert.That(resolved.IsTaskType(compilation.CreateArrayTypeSymbol(compilation.GetSpecialType(SpecialType.System_Int32)))).IsFalse();
    }

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
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = source, FixedCode = fixedSource, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 8 reference assemblies.</summary>
    /// <param name="source">The source with any diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAnalyzerAsync(string source)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = source, };

        await test.RunAsync(CancellationToken.None);
    }
}
