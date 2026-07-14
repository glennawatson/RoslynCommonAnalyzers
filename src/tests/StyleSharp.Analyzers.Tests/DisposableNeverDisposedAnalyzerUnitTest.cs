// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDisposable = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2410DisposableNeverDisposedAnalyzer>;
using VerifyDisposableFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2410DisposableNeverDisposedAnalyzer,
    StyleSharp.Analyzers.Sst2410DisposableNeverDisposedCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2410 (a created disposable that is never disposed).</summary>
public class DisposableNeverDisposedAnalyzerUnitTest
{
    /// <summary>A disposable local that is used and dropped.</summary>
    private const string UndisposedSource = """
        using System.IO;

        public sealed class C
        {
            public void M()
            {
                var {|SST2410:stream|} = new MemoryStream();
                stream.WriteByte(1);
            }
        }
        """;

    /// <summary>The undisposed source after the fix.</summary>
    private const string UndisposedFixed = """
        using System.IO;

        public sealed class C
        {
            public void M()
            {
                using var stream = new MemoryStream();
                stream.WriteByte(1);
            }
        }
        """;

    /// <summary>An async-disposable local dropped inside an async body.</summary>
    private const string AsyncUndisposedSource = """
        using System;
        using System.Threading.Tasks;

        public sealed class Connection : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => default;

            public void Use()
            {
            }
        }

        public sealed class C
        {
            public async Task M()
            {
                var {|SST2410:connection|} = new Connection();
                connection.Use();
                await Task.Yield();
            }
        }
        """;

    /// <summary>The async-disposable source after the fix.</summary>
    private const string AsyncUndisposedFixed = """
        using System;
        using System.Threading.Tasks;

        public sealed class Connection : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => default;

            public void Use()
            {
            }
        }

        public sealed class C
        {
            public async Task M()
            {
                await using var connection = new Connection();
                connection.Use();
                await Task.Yield();
            }
        }
        """;

    /// <summary>An async-only disposable dropped in a synchronous body, where 'await using' would not compile.</summary>
    private const string AsyncDisposableInSyncBodySource = """
        using System;
        using System.Threading.Tasks;

        public sealed class Connection : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => default;

            public void Use()
            {
            }
        }

        public sealed class C
        {
            public void M()
            {
                var {|SST2410:connection|} = new Connection();
                connection.Use();
            }
        }
        """;

    /// <summary>Verifies a created disposable that is used and dropped is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NeverDisposedIsReportedAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public void M()
                {
                    var {|SST2410:stream|} = new MemoryStream();
                    stream.WriteByte(1);
                }
            }
            """);

    /// <summary>Verifies a disposable that is created and never touched again is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NeverUsedIsReportedAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public void M()
                {
                    var {|SST2410:stream|} = new MemoryStream();
                }
            }
            """);

    /// <summary>Verifies a user-defined disposable is reported, not just the framework's.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedDisposableIsReportedAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Handle : IDisposable
            {
                public void Dispose()
                {
                }

                public void Use()
                {
                }
            }

            public sealed class C
            {
                public void M()
                {
                    var {|SST2410:handle|} = new Handle();
                    handle.Use();
                }
            }
            """);

    /// <summary>Verifies an async disposable that is never disposed is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncDisposableIsReportedAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class Connection : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => default;

                public void Use()
                {
                }
            }

            public sealed class C
            {
                public void M()
                {
                    var {|SST2410:connection|} = new Connection();
                    connection.Use();
                }
            }
            """);

    /// <summary>Verifies a disposable that is disposed is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposedIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public void M()
                {
                    var stream = new MemoryStream();
                    stream.WriteByte(1);
                    stream.Dispose();
                }
            }
            """);

    /// <summary>Verifies a disposable disposed in a finally block is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposedInFinallyIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public void M()
                {
                    var stream = new MemoryStream();
                    try
                    {
                        stream.WriteByte(1);
                    }
                    finally
                    {
                        stream.Dispose();
                    }
                }
            }
            """);

    /// <summary>Verifies an async disposable that is awaited to disposal is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposeAsyncIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class Connection : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => default;
            }

            public sealed class C
            {
                public async Task M()
                {
                    var connection = new Connection();
                    await connection.DisposeAsync();
                }
            }
            """);

    /// <summary>Verifies a using declaration is not reported — it is already disposal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingDeclarationIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public void M()
                {
                    using var stream = new MemoryStream();
                    stream.WriteByte(1);
                }
            }
            """);

    /// <summary>Verifies an await using declaration is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitUsingDeclarationIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class Connection : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => default;
            }

            public sealed class C
            {
                public async Task M()
                {
                    await using var connection = new Connection();
                    await Task.Yield();
                }
            }
            """);

    /// <summary>Verifies a using statement is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingStatementIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public void M()
                {
                    using (var stream = new MemoryStream())
                    {
                        stream.WriteByte(1);
                    }
                }
            }
            """);

    /// <summary>Verifies a disposable that is returned is not reported — the caller owns it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnedIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public Stream M()
                {
                    var stream = new MemoryStream();
                    stream.WriteByte(1);
                    return stream;
                }
            }
            """);

    /// <summary>Verifies a disposable stored in a field is not reported — the type owns it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignedToFieldIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                private Stream _stream;

                public Stream Current() => _stream;

                public void M()
                {
                    var stream = new MemoryStream();
                    _stream = stream;
                }
            }
            """);

    /// <summary>Verifies a disposable stored in a property is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignedToPropertyIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public Stream Current { get; set; }

                public void M()
                {
                    var stream = new MemoryStream();
                    Current = stream;
                }
            }
            """);

    /// <summary>Verifies a disposable copied into another local is not reported — that one may be disposed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignedToAnotherLocalIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public void M()
                {
                    var stream = new MemoryStream();
                    Stream other = stream;
                    other.Dispose();
                }
            }
            """);

    /// <summary>Verifies a disposable passed to a method is not reported — the callee may take ownership.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PassedToMethodIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public void M()
                {
                    var stream = new MemoryStream();
                    Consume(stream);
                }

                private static void Consume(Stream stream)
                {
                }
            }
            """);

    /// <summary>Verifies a disposable handed to a constructor is not reported — the new object owns it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PassedToConstructorIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public void M()
                {
                    var stream = new MemoryStream();
                    using var reader = new StreamReader(stream);
                    reader.ReadToEnd();
                }
            }
            """);

    /// <summary>Verifies a disposable added to a collection is not reported — the collection may own it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddedToCollectionIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.IO;

            public sealed class C
            {
                public void M(List<Stream> streams)
                {
                    var stream = new MemoryStream();
                    streams.Add(stream);
                }
            }
            """);

    /// <summary>Verifies a disposable that is yielded is not reported — the consumer owns it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task YieldReturnedIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.IO;

            public sealed class C
            {
                public IEnumerable<Stream> M()
                {
                    var stream = new MemoryStream();
                    yield return stream;
                }
            }
            """);

    /// <summary>Verifies a disposable captured by a lambda is not reported — the closure outlives the scan.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapturedByLambdaIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System;
            using System.IO;

            public sealed class C
            {
                public Action M()
                {
                    var stream = new MemoryStream();
                    return () => stream.WriteByte(1);
                }
            }
            """);

    /// <summary>Verifies a disposable captured by a local function is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapturedByLocalFunctionIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public void M()
                {
                    var stream = new MemoryStream();
                    Write();

                    void Write() => stream.WriteByte(1);
                }
            }
            """);

    /// <summary>Verifies a disposable struct is never reported; disposing a copy is not this rule's call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposableStructIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System;

            public struct Registration : IDisposable
            {
                public void Dispose()
                {
                }

                public void Use()
                {
                }
            }

            public sealed class C
            {
                public void M()
                {
                    var registration = new Registration();
                    registration.Use();
                }
            }
            """);

    /// <summary>Verifies a Task is never reported; it is disposable but must not be disposed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TaskIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public sealed class C
            {
                public void M()
                {
                    var task = new Task(() => { });
                    task.Start();
                }
            }
            """);

    /// <summary>Verifies a disposable that is not created with 'new' is not reported — a factory's result may be owned elsewhere.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactoryCreatedIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            using System.IO;

            public sealed class C
            {
                public void M(string path)
                {
                    var stream = File.OpenRead(path);
                    stream.ReadByte();
                }
            }
            """);

    /// <summary>Verifies a non-disposable local is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonDisposableIsCleanAsync()
        => await VerifyDisposable.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M()
                {
                    var value = new object();
                    value.ToString();
                }
            }
            """);

    /// <summary>Verifies the code fix turns the local into a using declaration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAddsUsingDeclarationAsync()
        => await VerifyDisposableFix.VerifyCodeFixAsync(UndisposedSource, UndisposedFixed);

    /// <summary>Verifies the code fix awaits the using declaration for an async-disposable in an async body.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAddsAwaitUsingDeclarationAsync()
        => await VerifyDisposableFix.VerifyCodeFixAsync(AsyncUndisposedSource, AsyncUndisposedFixed);

    /// <summary>Verifies no fix is offered for an async-only disposable in a synchronous body: the diagnostic stands, but 'await using' would not compile.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoCodeFixForAsyncDisposableInSyncBodyAsync()
        => await VerifyDisposableFix.VerifyCodeFixAsync(AsyncDisposableInSyncBodySource, AsyncDisposableInSyncBodySource);
}
