// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;

using VerifyDispose = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2300DisposePatternAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2300 (implement the disposal pattern correctly).</summary>
public class Sst2300DisposePatternAnalyzerUnitTest
{
    /// <summary>Verifies an unsealed disposable type with no <c>Dispose(bool)</c> is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnsealedTypeWithoutDisposeOverloadIsReportedAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            using System;

            public class {|SST2300:Connection|} : IDisposable
            {
                public void Dispose()
                {
                }
            }

            public abstract class {|SST2300:Resource|} : IDisposable
            {
                public abstract void Dispose();
            }
            """);

    /// <summary>Verifies a sealed type with no finalizer is complete with a plain <c>Dispose()</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>Nothing can derive from it and nothing else calls its cleanup, so the pattern is not asked for.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SealedTypeWithNoFinalizerIsCleanAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Connection : IDisposable
            {
                private bool _closed;

                public void Dispose() => _closed = true;
            }
            """);

    /// <summary>Verifies a public <c>Dispose(bool)</c> is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PublicDisposeOverloadIsReportedAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            using System;

            public class Connection : IDisposable
            {
                public void Dispose()
                {
                    Dispose(true);
                }

                public virtual void {|SST2300:Dispose|}(bool disposing)
                {
                }
            }
            """);

    /// <summary>Verifies a <c>Dispose()</c> that never chains to <c>Dispose(true)</c> is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DisposeThatDoesNotChainIsReportedAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            using System;

            public class Connection : IDisposable
            {
                public void {|SST2300:Dispose|}()
                {
                }

                protected virtual void Dispose(bool disposing)
                {
                }
            }
            """);

    /// <summary>Verifies a finalizable type whose <c>Dispose()</c> forgets to suppress finalization is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FinalizerWithoutSuppressFinalizeIsReportedAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            using System;

            public class Connection : IDisposable
            {
                ~Connection() => Dispose(false);

                public void {|SST2300:Dispose|}()
                {
                    Dispose(true);
                }

                protected virtual void Dispose(bool disposing)
                {
                }
            }
            """);

    /// <summary>Verifies the whole pattern, written out, is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CompletePatternIsCleanAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            using System;

            public class Connection : IDisposable
            {
                private bool _disposed;

                ~Connection() => Dispose(false);

                public void Dispose()
                {
                    Dispose(true);
                    GC.SuppressFinalize(this);
                }

                protected virtual void Dispose(bool disposing)
                {
                    if (!_disposed)
                    {
                        _disposed = true;
                    }
                }
            }
            """);

    /// <summary>Verifies the chained call still counts when it is guarded or qualified.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task QualifiedAndGuardedCallsAreFoundAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            using System;

            public class Connection : IDisposable
            {
                private bool _disposed;

                ~Connection() => Dispose(false);

                public void Dispose()
                {
                    if (!_disposed)
                    {
                        this.Dispose(true);
                    }

                    GC.SuppressFinalize(this);
                }

                protected virtual void Dispose(bool disposing) => _disposed = disposing;
            }
            """);

    /// <summary>Verifies a derived type inherits the pattern and is not asked to build it again.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DerivedTypeIsNotAskedForThePatternAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            using System;

            public class Connection : IDisposable
            {
                public void Dispose()
                {
                    Dispose(true);
                }

                protected virtual void Dispose(bool disposing)
                {
                }
            }

            public class PooledConnection : Connection
            {
                protected override void Dispose(bool disposing)
                {
                    base.Dispose(disposing);
                }
            }

            public class TrackedConnection : Connection, IDisposable
            {
            }
            """);

    /// <summary>Verifies a derived type that hides the base's <c>Dispose()</c> with a new one is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>A <c>using</c> on a base-typed variable dispatches statically to the base's <c>Dispose()</c>, not the hidden one.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DerivedTypeHidingBaseDisposeIsReportedAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            using System;

            public class Connection : IDisposable
            {
                public void Dispose()
                {
                    Dispose(true);
                }

                protected virtual void Dispose(bool disposing)
                {
                }
            }

            public class PooledConnection : Connection
            {
                public new void {|SST2300:Dispose|}()
                {
                }
            }
            """);

    /// <summary>Verifies a derived type that overrides a virtual base <c>Dispose()</c> is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DerivedTypeOverridingVirtualDisposeIsCleanAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            using System;

            public class Connection : IDisposable
            {
                public virtual void Dispose()
                {
                    Dispose(true);
                }

                protected virtual void Dispose(bool disposing)
                {
                }
            }

            public class PooledConnection : Connection
            {
                public override void Dispose()
                {
                    base.Dispose();
                }
            }
            """);

    /// <summary>Verifies an explicit <c>IDisposable.Dispose</c> implementation is read like any other.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExplicitDisposeImplementationIsReadAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            using System;

            public class Connection : IDisposable
            {
                ~Connection() => Dispose(false);

                void IDisposable.Dispose()
                {
                    Dispose(true);
                    GC.SuppressFinalize(this);
                }

                protected virtual void Dispose(bool disposing)
                {
                }
            }

            public class Session : IDisposable
            {
                void IDisposable.{|SST2300:Dispose|}()
                {
                }

                protected virtual void Dispose(bool disposing)
                {
                }
            }
            """);

    /// <summary>Verifies a struct is not measured: it cannot be derived from and cannot have a finalizer.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StructIsCleanAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            using System;

            public struct Handle : IDisposable
            {
                public void Dispose()
                {
                }
            }
            """);

    /// <summary>Verifies a record is skipped.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RecordIsCleanAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            using System;

            public record Session : IDisposable
            {
                public void Dispose()
                {
                }
            }
            """);

    /// <summary>Verifies a type that implements only <c>IAsyncDisposable</c> is out of scope.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AsyncDisposableOnlyIsCleanAsync()
    {
        var test = new VerifyDispose.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       using System;
                       using System.Threading.Tasks;

                       public class Pump : IAsyncDisposable
                       {
                           public ValueTask DisposeAsync() => default;
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a type that implements neither disposal contract is never looked at.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonDisposableTypeIsCleanAsync() =>
        VerifyDispose.VerifyAnalyzerAsync(
            """
            public class Connection
            {
                public void Dispose()
                {
                }
            }
            """);
}
