// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests finding the node a diagnostic was reported inside.</summary>
public class DiagnosticAncestorTests
{
    /// <summary>A method holding a statement nested inside an <c>if</c>.</summary>
    private const string Source = "class C { void M(bool b) { if (b) { M(false); } } }";

    /// <summary>Verifies the nearest enclosing node of the requested type is returned.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FindsTheNearestEnclosingNodeAsync()
    {
        var root = await CSharpSyntaxTree.ParseText(Source).GetRootAsync();
        var span = SpanOf("false");

        await Assert.That(DiagnosticAncestor.Find<InvocationExpressionSyntax>(root, span)!.ToString()).IsEqualTo("M(false)");
        await Assert.That(DiagnosticAncestor.Find<IfStatementSyntax>(root, span)!.Condition.ToString()).IsEqualTo("b");
        await Assert.That(DiagnosticAncestor.Find<MethodDeclarationSyntax>(root, span)!.Identifier.ValueText).IsEqualTo("M");
    }

    /// <summary>Verifies nothing is returned when no enclosing node has the requested type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NoEnclosingNodeReturnsNullAsync()
    {
        var root = await CSharpSyntaxTree.ParseText(Source).GetRootAsync();

        await Assert.That(DiagnosticAncestor.Find<SwitchStatementSyntax>(root, SpanOf("false"))).IsNull();
    }

    /// <summary>Gets the span of the first occurrence of some text in <see cref="Source"/>.</summary>
    /// <param name="text">The text to locate.</param>
    /// <returns>The text's span.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TextSpan SpanOf(string text) => new(Source.IndexOf(text, StringComparison.Ordinal), text.Length);
}
