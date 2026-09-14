// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the all-constant check over an array initializer.</summary>
public class InitializerConstantsTests
{
    /// <summary>Verifies every element must have a compile-time constant value, and an empty initializer qualifies.</summary>
    /// <param name="initializer">The array creation text.</param>
    /// <param name="expected">Whether every element is constant.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("new[] { 1, 2, 3 }", true)]
    [Arguments("new[] { 1, K, 1 + K }", true)]
    [Arguments("new int[] { }", true)]
    [Arguments("new[] { \"a\", nameof(C) }", true)]
    [Arguments("new[] { 1, p }", false)]
    [Arguments("new[] { F }", false)]
    [Arguments("new[] { 1, Load() }", false)]
    public async Task EveryElementMustBeConstantAsync(string initializer, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ const int K = 1; static int F; static int Load() => 0; object M(int p) => {initializer}; }}");
        var compilation = CSharpCompilation.Create(nameof(EveryElementMustBeConstantAsync), [tree], [RuntimeMetadataReferences.CoreLibrary]);
        var syntax = (await tree.GetRootAsync()).DescendantNodes().OfType<InitializerExpressionSyntax>().Single();

        await Assert.That(InitializerConstants.AreAllConstant(compilation.GetSemanticModel(tree), syntax, CancellationToken.None)).IsEqualTo(expected);
    }
}
