// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests placing leading trivia on the token that opens a rebuilt declaration.</summary>
public class LeadingTriviaPlacementTests
{
    /// <summary>Verifies the trivia lands on the attribute list, the first modifier, or the keyword, in that order of preference.</summary>
    /// <param name="accessorSource">The accessor declaration source.</param>
    /// <param name="expected">The accessor text after placement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("[A] private get;", "/*t*/[A] private get;")]
    [Arguments("private get;", "/*t*/private get;")]
    [Arguments("get;", "/*t*/get;")]
    public async Task PlacesTriviaOnTheOpeningTokenAsync(string accessorSource, string expected)
    {
        var accessor = ParseAccessor(accessorSource);
        var attributeLists = accessor.AttributeLists;
        var modifiers = accessor.Modifiers;
        var keyword = accessor.Keyword;

        LeadingTriviaPlacement.PlaceOnFirstToken(ref attributeLists, ref modifiers, ref keyword, SyntaxFactory.TriviaList(SyntaxFactory.Comment("/*t*/")));
        var rebuilt = accessor.Update(attributeLists, modifiers, keyword, accessor.Body, accessor.ExpressionBody, accessor.SemicolonToken);

        await Assert.That(rebuilt.ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Parses one accessor inside a property.</summary>
    /// <param name="accessorSource">The accessor declaration source.</param>
    /// <returns>The parsed accessor.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AccessorDeclarationSyntax ParseAccessor(string accessorSource) =>
        SyntaxFactory.ParseCompilationUnit($"class C {{ int P {{ {accessorSource} set; }} }}")
            .DescendantNodes()
            .OfType<AccessorDeclarationSyntax>()
            .First()
            .WithoutTrivia();
}
