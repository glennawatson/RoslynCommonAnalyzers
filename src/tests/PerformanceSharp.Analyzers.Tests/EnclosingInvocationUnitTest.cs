// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Direct tests for <see cref="EnclosingInvocation"/>, shared by the LINQ and collection code fixes.</summary>
public class EnclosingInvocationUnitTest
{
    /// <summary>A method holding nested calls.</summary>
    private const string Source = "class C { int M(int[] xs) => xs.Where(x => x > 0).Count(); }";

    /// <summary>Verifies the nearest enclosing call is found.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FindsTheNearestCallAsync()
    {
        var tree = CSharpSyntaxTree.ParseText(Source);
        var root = await tree.GetRootAsync();

        await Assert.That(EnclosingInvocation.Find(root, DiagnosticAt(tree, "x > 0"))!.ToString()).IsEqualTo("xs.Where(x => x > 0)");
        await Assert.That(EnclosingInvocation.Find(root, DiagnosticAt(tree, "Count"))!.ToString()).IsEqualTo("xs.Where(x => x > 0).Count()");
    }

    /// <summary>Verifies a node outside any call finds nothing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NodeOutsideAnyCallFindsNothingAsync()
    {
        var tree = CSharpSyntaxTree.ParseText(Source);
        var root = await tree.GetRootAsync();

        await Assert.That(EnclosingInvocation.Find(root, DiagnosticAt(tree, "int[]"))).IsNull();
    }

    /// <summary>Creates a diagnostic spanning the first occurrence of some text in <see cref="Source"/>.</summary>
    /// <param name="tree">The syntax tree the diagnostic belongs to.</param>
    /// <param name="text">The text the diagnostic covers.</param>
    /// <returns>The diagnostic.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Diagnostic DiagnosticAt(SyntaxTree tree, string text) =>
        Diagnostic.Create(CollectionRules.UseNaturalOrder, Location.Create(tree, new(Source.IndexOf(text, StringComparison.Ordinal), text.Length)));
}
