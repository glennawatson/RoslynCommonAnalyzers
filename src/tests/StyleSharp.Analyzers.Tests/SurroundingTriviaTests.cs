// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the detection of comments and other non-whitespace trivia around a node.</summary>
public class SurroundingTriviaTests
{
    /// <summary>Verifies spaces and line breaks are ignored while a leading or trailing comment is found.</summary>
    /// <param name="source">The statement with its trivia.</param>
    /// <param name="expected">Whether non-whitespace trivia surrounds the statement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("x = 1;", false)]
    [Arguments("   x = 1;   \n", false)]
    [Arguments("// note\nx = 1;", true)]
    [Arguments("x = 1; // note", true)]
    [Arguments("/* note */ x = 1;", true)]
    public async Task FindsAnythingButWhitespaceAsync(string source, bool expected)
    {
        var statement = SyntaxFactory.ParseStatement(source);

        await Assert.That(SurroundingTrivia.HasNonWhitespace(statement)).IsEqualTo(expected);
    }
}
