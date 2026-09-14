// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
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

    /// <summary>Verifies namespace-qualified and alias-qualified framework calls are reported.</summary>
    /// <param name="call">The qualified framework invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("System.GC.SuppressFinalize(this)")]
    [Arguments("global::System.GC.SuppressFinalize(this)")]
    [Arguments("runtime::GC.SuppressFinalize(this)")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task QualifiedFrameworkCallIsReportedAsync(string call) =>
        Verify.VerifyAnalyzerAsync($$"""
            using runtime = System;
            sealed class C { void Dispose() { {|PSH1008:{{call}}|}; } }
            """);

    /// <summary>Verifies only a direct GC-named call with exactly the receiver argument passes the syntax gate.</summary>
    /// <param name="statement">The unrelated invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("System.GC.Collect();")]
    [Arguments("System.GC.SuppressFinalize(new object());")]
    [Arguments("System.GC.SuppressFinalize((this));")]
    [Arguments("SuppressFinalize(this);")]
    [Arguments("collector.SuppressFinalize(this);")]
    [Arguments("(collector).SuppressFinalize(this);")]
    [Arguments("Other.Collector.SuppressFinalize(this);")]
    [Arguments("global::Collector.SuppressFinalize(this);")]
    [Arguments("GC.SuppressFinalize();")]
    [Arguments("GC.SuppressFinalize(this, this);")]
    [Arguments("GC.SuppressFinalize(this);")]
    [Arguments("global::GC.SuppressFinalize(this);")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SyntaxAndSymbolLookalikesAreCleanAsync(string statement) =>
        Verify.VerifyAnalyzerAsync($$"""
            using static System.GC;
            class Collector { public static void SuppressFinalize(object value) { } }
            class GC { public static void SuppressFinalize(params object[] values) { } }
            namespace Other { class Collector { public static void SuppressFinalize(object value) { } } }
            class Receiver { public void SuppressFinalize(object value) { } }
            sealed class C
            {
                Receiver collector = new Receiver();
                void Dispose() { {{statement}} }
            }
            """);

    /// <summary>Verifies an interface default member cannot establish that all implementers lack finalizers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InterfaceDefaultDisposeIsCleanAsync() =>
        new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = "interface I { void Dispose() { System.GC.SuppressFinalize(this); } }" }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a member merely named Finalize is not treated as a destructor.</summary>
    /// <param name="member">The ordinary method or field with the reserved name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public void Finalize(int value) { }")]
    [Arguments("public int Finalize;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task OrdinaryFinalizeMemberDoesNotPreventReportAsync(string member) =>
        Verify.VerifyAnalyzerAsync($$"""
            sealed class C { {{member}} void Dispose() { {|PSH1008:System.GC.SuppressFinalize(this)|}; } }
            """);

    /// <summary>Verifies unresolved invocations and invalid containing contexts do not produce analyzer diagnostics.</summary>
    /// <param name="source">The incomplete source being edited.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System; sealed class C { void Dispose() { GC.SuppressFinalize<int>(this); } }")]
    [Arguments("sealed class C { void Dispose() { Missing.GC.SuppressFinalize(this); } }")]
    [Arguments("[assembly: A(System.GC.SuppressFinalize(this))] class A : System.Attribute { public A(object value) { } }")]
    public async Task UnresolvedCallsAreCleanAsync(string source)
    {
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1008UselessSuppressFinalizeAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a bound lookalike remains clean when the target framework has no System.GC.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingFrameworkGcTypeIsCleanAsync()
    {
        const string Source = """
            namespace System { public class Object { } public struct Void { } }
            class GC { public static void SuppressFinalize(object value) { } }
            sealed class C { void Dispose() { GC.SuppressFinalize(this); } }
            """;
        var tree = CSharpSyntaxTree.ParseText(Source);
        var compilation = CSharpCompilation.Create(nameof(Test), [tree]);
        var invocation = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        await Assert.That(compilation.GetSemanticModel(tree).GetSymbolInfo(invocation).Symbol).IsNotNull();
        await Assert.That(compilation.GetTypeByMetadataName("System.GC")).IsNull();
        var diagnostics = await compilation.WithAnalyzers([new Psh1008UselessSuppressFinalizeAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a sealed finalizer-free type is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SealedFinalizerFreeTypeIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(SealedNoFinalizerSource);

    /// <summary>Verifies a sealed type with a finalizer is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SealedTypeWithFinalizerIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnsealedTypeIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SealedTypeWithBaseFinalizerIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StructDisposeIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixRemovesStatementAsync() =>
        Verify.VerifyCodeFixAsync(SealedNoFinalizerSource, SealedNoFinalizerFixed);
}
