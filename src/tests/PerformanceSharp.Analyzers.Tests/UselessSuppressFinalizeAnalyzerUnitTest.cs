// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1008UselessSuppressFinalizeAnalyzer,
    PerformanceSharp.Analyzers.Psh1008UselessSuppressFinalizeCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1008UselessSuppressFinalizeAnalyzer"/> (PSH1008 useless SuppressFinalize).</summary>
public class UselessSuppressFinalizeAnalyzerUnitTest
{
    /// <summary>A sealed dispose pattern with no finalizer anywhere.</summary>
    private const string SealedNoFinalizerSource = """
        using System;

        public sealed class C : IDisposable
        {
            public void Dispose()
            {
                {|PSH1008:GC.SuppressFinalize(this)|};
            }
        }
        """;

    /// <summary>The sealed dispose pattern after the fix.</summary>
    private const string SealedNoFinalizerFixed = """
        using System;

        public sealed class C : IDisposable
        {
            public void Dispose()
            {
            }
        }
        """;

    /// <summary>Verifies a sealed finalizer-free type is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SealedFinalizerFreeTypeIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(SealedNoFinalizerSource);

    /// <summary>Verifies a sealed type with a finalizer is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SealedTypeWithFinalizerIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C : IDisposable
            {
                ~C()
                {
                }

                public void Dispose()
                {
                    GC.SuppressFinalize(this);
                }
            }
            """);

    /// <summary>Verifies an unsealed type is clean because a derived type may add a finalizer.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnsealedTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C : IDisposable
            {
                public void Dispose()
                {
                    GC.SuppressFinalize(this);
                }
            }
            """);

    /// <summary>Verifies a sealed type whose base declares a finalizer is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SealedTypeWithBaseFinalizerIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class B
            {
                ~B()
                {
                }
            }

            public sealed class C : B, IDisposable
            {
                public void Dispose()
                {
                    GC.SuppressFinalize(this);
                }
            }
            """);

    /// <summary>Verifies a struct dispose is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StructDisposeIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public struct S : IDisposable
            {
                public void Dispose()
                {
                    {|PSH1008:GC.SuppressFinalize(this)|};
                }
            }
            """);

    /// <summary>Verifies the fix removes the whole statement.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixRemovesStatementAsync()
        => await Verify.VerifyCodeFixAsync(SealedNoFinalizerSource, SealedNoFinalizerFixed);
}
