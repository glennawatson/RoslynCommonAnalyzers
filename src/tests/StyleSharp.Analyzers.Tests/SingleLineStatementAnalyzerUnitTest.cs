// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Helper-level tests for single-line statement analysis fast paths.</summary>
public sealed class SingleLineStatementAnalyzerUnitTest
{
    /// <summary>Verifies a brace pair on one physical line is recognized as single-line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsSingleLineBlockRecognizesInlineBracePairAsync()
    {
        var block = ParseBlock("class C { void M() { if (true) { return; } } }");
        var text = await block.SyntaxTree.GetTextAsync();

        await Assert.That(Sst1501SingleLineStatementAnalyzer.IsSingleLineBlock(text, block)).IsTrue();
    }

    /// <summary>Verifies a multi-line brace pair is not treated as a single-line block.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsSingleLineBlockRejectsMultiLineBracePairAsync()
    {
        var block = ParseBlock(
            """
            class C { void M() { if (true)
            {
                return;
            } } }
            """);
        var text = await block.SyntaxTree.GetTextAsync();

        await Assert.That(Sst1501SingleLineStatementAnalyzer.IsSingleLineBlock(text, block)).IsFalse();
    }

    /// <summary>Parses the innermost block from the supplied compilation unit source.</summary>
    /// <param name="source">The source containing the target block.</param>
    /// <returns>The parsed block.</returns>
    private static BlockSyntax ParseBlock(string source)
    {
        var type = (TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0];
        var method = (MethodDeclarationSyntax)type.Members[0];
        var ifStatement = (IfStatementSyntax)method.Body!.Statements[0];
        return (BlockSyntax)ifStatement.Statement;
    }
}
