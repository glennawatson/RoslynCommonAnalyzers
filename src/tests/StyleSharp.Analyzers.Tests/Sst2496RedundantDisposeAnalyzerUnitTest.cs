// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2496RedundantDisposeAnalyzer,
    StyleSharp.Analyzers.Sst2496RedundantDisposeCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2496 (an explicit dispose of a using-governed local).</summary>
public class Sst2496RedundantDisposeAnalyzerUnitTest
{
    /// <summary>The disposable helper shared by the test sources.</summary>
    private const string Disposable = """

        public sealed class D : System.IDisposable
        {
            public void Dispose() { }
            public void Close() { }
            public void Use() { }
        }
        """;

    /// <summary>Verifies an explicit Dispose on a using-declaration local is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposeOnUsingDeclarationIsRemovedAsync()
    {
        const string Source = """
            public sealed class C
            {
                public void M()
                {
                    using var d = new D();
                    d.Use();
                    {|SST2496:d.Dispose()|};
                }
            }
            """ + Disposable;
        const string Fixed = """
            public sealed class C
            {
                public void M()
                {
                    using var d = new D();
                    d.Use();
                }
            }
            """ + Disposable;
        await Verify.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies an explicit Close on a using-statement local is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CloseOnUsingStatementIsRemovedAsync()
    {
        const string Source = """
            public sealed class C
            {
                public void M()
                {
                    using (var d = new D())
                    {
                        d.Use();
                        {|SST2496:d.Close()|};
                    }
                }
            }
            """ + Disposable;
        const string Fixed = """
            public sealed class C
            {
                public void M()
                {
                    using (var d = new D())
                    {
                        d.Use();
                    }
                }
            }
            """ + Disposable;
        await Verify.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies disposing a plain local that no using governs is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposeOnPlainLocalIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M()
                {
                    var d = new D();
                    d.Use();
                    d.Dispose();
                }
            }
            """ + Disposable);
}
