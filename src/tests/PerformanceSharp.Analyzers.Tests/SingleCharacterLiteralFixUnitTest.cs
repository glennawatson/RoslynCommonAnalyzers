// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Direct tests for <see cref="SingleCharacterLiteralFix"/>, shared by the PSH1201 and PSH1202 code fixes.</summary>
public class SingleCharacterLiteralFixUnitTest
{
    /// <summary>A method passing one-character strings to string and builder calls.</summary>
    private const string Source = "class C { bool M(string s, System.Text.StringBuilder b) => s.StartsWith(\"x\") && b.Append(\"'\").ToString().Length == 1; }";

    /// <summary>Verifies the literal resolves and converts, escaping the character where a char literal needs it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ResolvesAndConvertsTheLiteralAsync()
    {
        var tree = CSharpSyntaxTree.ParseText(Source);
        var root = await tree.GetRootAsync();

        var found = SingleCharacterLiteralFix.TryFind(root, DiagnosticAt(tree, "\"'\""), out var literal);

        await Assert.That(found).IsTrue();
        await Assert.That(SingleCharacterLiteralFix.ToCharacterLiteral(literal!).ToFullString()).IsEqualTo("'\\''");
    }

    /// <summary>Verifies the literal's trivia moves onto the char literal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConversionKeepsTheLiteralTriviaAsync()
    {
        var literal = (Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax)SyntaxFactory.ParseExpression("/*a*/\"x\"/*b*/");

        await Assert.That(SingleCharacterLiteralFix.ToCharacterLiteral(literal).ToFullString()).IsEqualTo("/*a*/'x'/*b*/");
    }

    /// <summary>Verifies a diagnostic on anything other than a single-character literal resolves nothing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherNodesResolveNothingAsync()
    {
        var tree = CSharpSyntaxTree.ParseText(Source);
        var root = await tree.GetRootAsync();

        var found = SingleCharacterLiteralFix.TryFind(root, DiagnosticAt(tree, "Length"), out var literal);

        await Assert.That(found).IsFalse();
        await Assert.That(literal).IsNull();
    }

    /// <summary>Creates a diagnostic spanning the first occurrence of some text in <see cref="Source"/>.</summary>
    /// <param name="tree">The syntax tree the diagnostic belongs to.</param>
    /// <param name="text">The text the diagnostic covers.</param>
    /// <returns>The diagnostic.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Diagnostic DiagnosticAt(SyntaxTree tree, string text) =>
        Diagnostic.Create(StringRules.UseCharOverload, Location.Create(tree, new(Source.IndexOf(text, StringComparison.Ordinal), text.Length)));
}
