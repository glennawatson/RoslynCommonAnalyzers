// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using NSubstitute;
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
                    using (var d = new System.IO.MemoryStream())
                    {
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
                    using (var d = new System.IO.MemoryStream())
                    {
                    }
                }
            }
            """ + Disposable;
        await Verify.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies closing a contour on a disposable builder preserves its geometry.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ContourCloseOnUsingLocalIsCleanAsync() => Verify.VerifyAnalyzerAsync(
        $"class C {{ void M() {{ using var builder = new D(); builder.Close(); }} }}{Disposable}");

    /// <summary>Verifies framework disposal aliases remain reported for using-owned resources.</summary>
    /// <param name="creation">The framework resource to construct.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("new System.IO.MemoryStream()")]
    [Arguments("new System.IO.StringReader(\"text\")")]
    [Arguments("new System.IO.StringWriter()")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task FrameworkCloseIsRemovedAsync(string creation) => Verify.VerifyCodeFixAsync(
        $"class C {{ void M() {{ using var resource = {creation}; {{|SST2496:resource.Close()|}}; }} }}",
        $"class C {{ void M() {{ using var resource = {creation}; }} }}");

    /// <summary>Verifies only overriding a framework Close method preserves its disposal contract.</summary>
    /// <param name="modifier">Whether Close overrides or hides the framework method.</param>
    /// <param name="reported">Whether Close retains the framework disposal contract.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("override", true)]
    [Arguments("new", false)]
    public Task DerivedCloseUsesTheResolvedMethodAsync(string modifier, bool reported)
    {
        var invocation = reported ? "{|SST2496:resource.Close()|}" : "resource.Close()";
        return Verify.VerifyAnalyzerAsync($"class D : System.IO.MemoryStream {{ public {modifier} void Close() {{ }} }} class C {{ void M() {{ using var resource = new D(); {invocation}; }} }}");
    }

    /// <summary>Verifies value-returning Close operations are preserved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ValueReturningCloseIsCleanAsync() => Verify.VerifyAnalyzerAsync(
        "class D : System.IDisposable { public void Dispose() { } public int Close() => 1; } class C { void M() { using var resource = new D(); resource.Close(); } }");

    /// <summary>Verifies a for-loop declaration does not transfer disposal ownership.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DisposeOnForLoopLocalIsCleanAsync() => Verify.VerifyAnalyzerAsync(
        $"class C {{ void M() {{ for (D resource = new D();;) {{ resource.Dispose(); break; }} }} }}{Disposable}");

    /// <summary>Verifies a local without a source declaration cannot be owned by using.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LocalWithoutDeclarationIsNotUsingAsync()
    {
        var local = Substitute.For<ILocalSymbol>();
        _ = local.DeclaringSyntaxReferences.Returns([]);
        await Assert.That(Sst2496RedundantDisposeAnalyzer.IsUsingLocal(local)).IsFalse();
    }

    /// <summary>Verifies disposing a plain local that no using governs is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DisposeOnPlainLocalIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
