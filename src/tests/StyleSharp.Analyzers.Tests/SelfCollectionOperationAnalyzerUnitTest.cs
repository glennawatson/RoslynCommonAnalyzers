// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using VerifySelf = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2419SelfCollectionOperationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2419 (a set or list operation applied to itself).</summary>
public class SelfCollectionOperationAnalyzerUnitTest
{
    /// <summary>Verifies each set operation is recognized on the interface itself.</summary>
    /// <param name="operation">The set operation to invoke.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Arguments("UnionWith")]
    [Arguments("IntersectWith")]
    [Arguments("ExceptWith")]
    [Arguments("SymmetricExceptWith")]
    [Arguments("SetEquals")]
    [Arguments("IsSubsetOf")]
    [Arguments("IsSupersetOf")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SetInterfaceSelfOperationIsReportedAsync(string operation) =>
        VerifySelf.VerifyAnalyzerAsync($$"""
            using System.Collections.Generic;
            class C
            {
                void M(ISet<int> set) { {|SST2419:set.{{operation}}(set)|}; }
            }
            """);

    /// <summary>Verifies the list insertion operation reads its second argument.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ListInsertionUsesSecondArgumentAsync() =>
        VerifySelf.VerifyAnalyzerAsync("""
            using System.Collections.Generic;
            class C
            {
                void M(List<int> items, List<int> other)
                {
                    {|SST2419:items.InsertRange(0, items)|};
                    items.InsertRange(0, other);
                }
            }
            """);

    /// <summary>Verifies list-interface receivers and constrained set receivers satisfy the collection contract.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InterfaceAndGenericCollectionReceiversAreReportedAsync() =>
        VerifySelf.VerifyAnalyzerAsync("""
            using System.Collections.Generic;
            static class Extensions
            {
                public static void AddRange<T>(this IList<T> list, IEnumerable<T> values) { }
            }
            class C
            {
                void M<T>(IList<int> items, T set) where T : ISet<int>
                {
                    {|SST2419:items.AddRange(items)|};
                    {|SST2419:set.UnionWith(set)|};
                }
            }
            """);

    /// <summary>Verifies matching method names on noncollection receivers remain clean.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnrelatedMethodsAndMissingArgumentsAreCleanAsync() =>
        VerifySelf.VerifyAnalyzerAsync("""
            using System;
            class C : IDisposable
            {
                public void Dispose() { }
                void UnionWith(C other) { }
                void UnionWith() { }
                void InsertRange(int index) { }
                void M(C other)
                {
                    other.UnionWith(other);
                    other.UnionWith();
                    other.InsertRange(0);
                    other.Dispose();
                    Dispose();
                }
            }
            """);

    /// <summary>Verifies unresolved receivers and method groups do not produce diagnostics.</summary>
    /// <param name="receiver">The incomplete receiver expression.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Arguments("missing")]
    [Arguments("M")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnresolvedReceiverIsCleanAsync(string receiver) =>
        new VerifySelf.Test { TestCode = $$"""class C { void M() { {{receiver}}.UnionWith({{receiver}}); } }""", CompilerDiagnostics = CompilerDiagnostics.None }
            .RunAsync(CancellationToken.None);

    /// <summary>Verifies similarly named operations are not mistaken for the collection methods.</summary>
    /// <param name="operation">The unrelated method name.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Arguments("UnionWitx")]
    [Arguments("IntersectWitx")]
    [Arguments("ExceptWitx")]
    [Arguments("SymmetricExceptWitx")]
    [Arguments("SetEqualx")]
    [Arguments("IsSubsetOx")]
    [Arguments("IsSupersetOx")]
    [Arguments("AddRangx")]
    [Arguments("InsertRangx")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SimilarlyNamedMethodIsCleanAsync(string operation) =>
        VerifySelf.VerifyAnalyzerAsync($$"""
            class C
            {
                void {{operation}}(C other) { }
                void M(C other) { other.{{operation}}(other); }
            }
            """);

    /// <summary>Verifies a compilation without collection interfaces safely ignores a candidate call.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task MissingCollectionInterfacesAreCleanAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { void UnionWith(C other) { } void M(C c) { c.UnionWith(c); } }");
        var compilation = CSharpCompilation.Create(nameof(MissingCollectionInterfacesAreCleanAsync), [tree]);
        var diagnostics = await compilation.WithAnalyzers([new Sst2419SelfCollectionOperationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a set unioned with itself is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SetUnionedWithItselfIsReportedAsync() =>
        VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(HashSet<int> set) => {|SST2419:set.UnionWith(set)|};
            }
            """);

    /// <summary>Verifies a list adding its own range is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ListAddingItsOwnRangeIsReportedAsync() =>
        VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(List<int> items) => {|SST2419:items.AddRange(items)|};
            }
            """);

    /// <summary>Verifies a set excepted with itself is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SetExceptedWithItselfIsReportedAsync() =>
        VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(HashSet<int> set) => {|SST2419:set.ExceptWith(set)|};
            }
            """);

    /// <summary>Verifies an operation on two different collections is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DifferentCollectionsAreCleanAsync() =>
        VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(HashSet<int> a, HashSet<int> b) => a.UnionWith(b);
            }
            """);

    /// <summary>Verifies a call-valued receiver, which may return two different collections, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CallValuedReceiverIsCleanAsync() =>
        VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M() => Get().UnionWith(Get());

                private HashSet<int> Get() => new HashSet<int>();
            }
            """);
}
