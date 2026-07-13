// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDispose = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2300DisposePatternAnalyzer,
    StyleSharp.Analyzers.Sst2300DisposePatternCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2300DisposePatternCodeFixProvider"/> (SST2300 disposal pattern).</summary>
public class Sst2300DisposePatternCodeFixUnitTest
{
    /// <summary>A finalizable type whose block-bodied <c>Dispose()</c> never suppresses finalization.</summary>
    private const string BlockBodySource = """
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
        """;

    /// <summary>The block-bodied source after the fix.</summary>
    private const string BlockBodyFixed = """
        using System;

        public class Connection : IDisposable
        {
            ~Connection() => Dispose(false);

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
            }
        }
        """;

    /// <summary>A finalizable type whose expression-bodied <c>Dispose()</c> never suppresses finalization.</summary>
    private const string ExpressionBodySource = """
        using System;

        public class Connection : IDisposable
        {
            ~Connection() => Dispose(false);

            public void {|SST2300:Dispose|}() => Dispose(true);

            protected virtual void Dispose(bool disposing)
            {
            }
        }
        """;

    /// <summary>The expression-bodied source after the fix, which now needs a block.</summary>
    private const string ExpressionBodyFixed = """
        using System;

        public class Connection : IDisposable
        {
            ~Connection() => Dispose(false);

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
            }
        }
        """;

    /// <summary>A type that can be derived from, whose <c>Dispose(bool)</c> is public.</summary>
    private const string PublicOverloadSource = """
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
        """;

    /// <summary>The public-overload source after the fix.</summary>
    private const string PublicOverloadFixed = """
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
        """;

    /// <summary>A sealed type whose <c>Dispose(bool)</c> is public, where nothing can override it.</summary>
    private const string SealedOverloadSource = """
        using System;

        public sealed class Connection : IDisposable
        {
            ~Connection() => Dispose(false);

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            public void {|SST2300:Dispose|}(bool disposing)
            {
            }
        }
        """;

    /// <summary>The sealed-overload source after the fix.</summary>
    private const string SealedOverloadFixed = """
        using System;

        public sealed class Connection : IDisposable
        {
            ~Connection() => Dispose(false);

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
            }
        }
        """;

    /// <summary>A file with no <c>using System;</c>, where the short call would not bind.</summary>
    private const string UnqualifiedSource = """
        public class Connection : System.IDisposable
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
        """;

    /// <summary>The unqualified source after the fix, which keeps the call qualified so it compiles.</summary>
    private const string UnqualifiedFixed = """
        public class Connection : System.IDisposable
        {
            ~Connection() => Dispose(false);

            public void Dispose()
            {
                Dispose(true);
                System.GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
            }
        }
        """;

    /// <summary>Verifies the fix appends the suppression call to a block-bodied <c>Dispose()</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AddsSuppressFinalizeToBlockBodyAsync()
        => await VerifyDispose.VerifyCodeFixAsync(BlockBodySource, BlockBodyFixed);

    /// <summary>Verifies an expression-bodied <c>Dispose()</c> becomes a block, because it now says two things.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExpressionBodiedDisposeBecomesABlockAsync()
        => await VerifyDispose.VerifyCodeFixAsync(ExpressionBodySource, ExpressionBodyFixed);

    /// <summary>Verifies a public <c>Dispose(bool)</c> becomes protected virtual on a type that can be derived from.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PublicOverloadBecomesProtectedVirtualAsync()
        => await VerifyDispose.VerifyCodeFixAsync(PublicOverloadSource, PublicOverloadFixed);

    /// <summary>Verifies a public <c>Dispose(bool)</c> becomes private on a sealed type, which nothing can override.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PublicOverloadBecomesPrivateOnASealedTypeAsync()
        => await VerifyDispose.VerifyCodeFixAsync(SealedOverloadSource, SealedOverloadFixed);

    /// <summary>Verifies the emitted call stays qualified where the short name would not bind.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EmittedCallBindsWithoutAUsingDirectiveAsync()
        => await VerifyDispose.VerifyCodeFixAsync(UnqualifiedSource, UnqualifiedFixed);
}
