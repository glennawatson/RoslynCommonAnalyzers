// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Direct tests for <see cref="TypeNameLookup"/>, shared by the PSH1307 and PSH1223 code fixes.</summary>
public class TypeNameLookupUnitTest
{
    /// <summary>The namespace <c>Volatile</c> lives in.</summary>
    private const string ThreadingNamespace = "System.Threading";

    /// <summary>The simple name the lookups probe.</summary>
    private const string VolatileName = "Volatile";

    /// <summary>Verifies the simple name binds only where the namespace is imported, and only into that namespace.</summary>
    /// <param name="source">The document source.</param>
    /// <param name="containingNamespace">The namespace the name must resolve into.</param>
    /// <param name="expected">Whether the simple name binds there.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("using System.Threading; class C { void M() { } }", ThreadingNamespace, true)]
    [Arguments("using System.Threading; class C { void M() { } }", "System.Text", false)]
    [Arguments("class C { void M() { } }", ThreadingNamespace, false)]
    public async Task ResolvesOnlyIntoTheNamespaceAsync(string source, string containingNamespace, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(nameof(TypeNameLookupUnitTest), [tree], RuntimeMetadataReferences.Platform);
        var model = compilation.GetSemanticModel(tree);
        var position = source.IndexOf("void", StringComparison.Ordinal);

        await Assert.That(TypeNameLookup.ResolvesIn(model, position, VolatileName, containingNamespace)).IsEqualTo(expected);
    }
}
