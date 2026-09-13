// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests extraction and replacement of generic declaration constraints.</summary>
public class GenericConstraintLayoutTests
{
    /// <summary>The number of constrained parameters in each fixture.</summary>
    private const int ExpectedParameterCount = 2;

    /// <summary>Checks every supported declaration retains its parameters and accepts reordered clauses.</summary>
    /// <param name="source">A declaration carrying two constraints.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("class C<T, U> where U : class where T : struct {}")]
    [Arguments("class C { void M<T, U>() where U : class where T : struct {} }")]
    [Arguments("delegate void D<T, U>() where U : class where T : struct;")]
    [Arguments("class C { void M() { void L<T, U>() where U : class where T : struct {} } }")]
    public async Task SupportedDeclarationConstraintsCanBeReorderedAsync(string source)
    {
        var root = SyntaxFactory.ParseCompilationUnit(source);
        var node = root.DescendantNodes().OfType<TypeParameterListSyntax>().Single().Parent!;
        await Assert.That(GenericConstraintLayout.TryGet(node, out var parameters, out var clauses)).IsTrue();
        await Assert.That(parameters!.Parameters.Count).IsEqualTo(ExpectedParameterCount);
        await Assert.That(GenericConstraintLayout.PositionOf(parameters, "U")).IsEqualTo(1);
        await Assert.That(GenericConstraintLayout.PositionOf(parameters, "missing")).IsEqualTo(-1);
        var changed = GenericConstraintLayout.WithConstraintClauses(node, SyntaxFactory.List(clauses.Reverse()));
        await Assert.That(GenericConstraintLayout.TryGet(changed, out _, out var reordered)).IsTrue();
        await Assert.That(reordered[0].Name.Identifier.ValueText).IsEqualTo("T");
        await Assert.That(reordered[1].Name.Identifier.ValueText).IsEqualTo("U");
    }

    /// <summary>Checks unsupported syntax is returned unchanged and has no generic layout.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task UnsupportedNodeHasNoConstraintsAsync()
    {
        var node = SyntaxFactory.ParseStatement("return;");
        await Assert.That(GenericConstraintLayout.TryGet(node, out var parameters, out var clauses)).IsFalse();
        await Assert.That(parameters).IsNull();
        await Assert.That(clauses.Count).IsEqualTo(0);
        await Assert.That(GenericConstraintLayout.WithConstraintClauses(node, default)).IsSameReferenceAs(node);
    }

    /// <summary>Checks non-generic declarations remain supported without manufacturing a parameter list.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task NonGenericTypeHasNoParameterListAsync()
    {
        var node = SyntaxFactory.ParseMemberDeclaration("class C {}")!;
        await Assert.That(GenericConstraintLayout.TryGet(node, out var parameters, out var clauses)).IsTrue();
        await Assert.That(parameters).IsNull();
        await Assert.That(clauses.Count).IsEqualTo(0);
    }
}
