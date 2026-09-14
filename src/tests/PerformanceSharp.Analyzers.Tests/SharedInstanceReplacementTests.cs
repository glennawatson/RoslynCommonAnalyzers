// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests whether an allocation can be rewritten into a read of a shared instance on its type.</summary>
public class SharedInstanceReplacementTests
{
    /// <summary>Verifies an explicit name is reused, a non-name type is refused, and a target-typed allocation needs the simple name in scope.</summary>
    /// <param name="source">A compilation unit with one object creation.</param>
    /// <param name="expected">Whether a compiling replacement exists.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System; class C { object A = new Random(); }", true)]
    [Arguments("class C { object A = new System.Random(); }", true)]
    [Arguments("class C { object A = new global::System.Random(); }", true)]
    [Arguments("class C { object A = new int?(); }", false)]
    [Arguments("using System; class C { Random A = new(); }", true)]
    [Arguments("class C { System.Random A = new(); }", false)]
    [Arguments("using System; class Random { } class C { System.Random A = new(); }", false)]
    public async Task ReplacementNeedsANameThatResolvesToTheTypeAsync(string source, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(nameof(ReplacementNeedsANameThatResolvesToTheTypeAsync), [tree], RuntimeMetadataReferences.Platform);
        var creation = (await tree.GetRootAsync()).DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>().Single();
        var random = compilation.GetTypeByMetadataName("System.Random")!;

        await Assert.That(SharedInstanceReplacement.CanWriteReplacement(creation, compilation.GetSemanticModel(tree), random, nameof(Random), CancellationToken.None))
            .IsEqualTo(expected);
    }
}
