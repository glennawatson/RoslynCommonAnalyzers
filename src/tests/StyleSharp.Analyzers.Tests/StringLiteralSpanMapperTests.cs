// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests mapping decoded literal offsets back to source escapes.</summary>
public class StringLiteralSpanMapperTests
{
    /// <summary>Verifies source spans include the complete escape sequence and leading token offset.</summary>
    /// <param name="text">The written token.</param>
    /// <param name="start">The decoded start offset.</param>
    /// <param name="length">The decoded length.</param>
    /// <param name="expected">The source text selected by the mapped span.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("\"abc\"", 1, 1, "b")]
    [Arguments("\"\\nX\"", 0, 1, "\\n")]
    [Arguments("\"\\u0041X\"", 0, 1, "\\u0041")]
    [Arguments("\"\\U00000041X\"", 0, 1, "\\U00000041")]
    [Arguments("\"\\x1X\"", 0, 1, "\\x1")]
    [Arguments("\"\\x12afX\"", 0, 1, "\\x12af")]
    [Arguments("\"\\xABCFX\"", 1, 1, "X")]
    [Arguments("\"\\x\"", 0, 1, "\\x")]
    [Arguments("\"\\xg\"", 0, 1, "\\x")]
    [Arguments("\"\\x:\"", 0, 1, "\\x")]
    [Arguments("\"\\x1", 0, 1, "\\x1")]
    [Arguments("\"\\", 0, 1, "\\")]
    [Arguments("@\"a\"\"b\"", 1, 1, "\"\"")]
    [Arguments("@\"a\"b", 1, 1, "\"")]
    [Arguments("@\"a\"", 1, 1, "\"")]
    [Arguments("\"abc\"", 0, 0, "")]
    public async Task DecodedSpanSelectsWrittenEscapeAsync(string text, int start, int length, string expected)
    {
        var token = SyntaxFactory.Literal(SyntaxFactory.TriviaList(SyntaxFactory.Space), text, string.Empty, default);
        var literal = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, token);
        var mapped = StringLiteralSpanMapper.TryMap(literal, start, length, out var span);
        await Assert.That(mapped).IsTrue();
        await Assert.That(literal.ToFullString().Substring(span.Start, span.Length)).IsEqualTo(expected);
    }

    /// <summary>Verifies unsupported tokens and offsets outside the written token cannot be mapped.</summary>
    /// <param name="text">The written token.</param>
    /// <param name="start">The decoded start offset.</param>
    /// <param name="length">The decoded length.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", 0, 1)]
    [Arguments("@", 0, 1)]
    [Arguments("abc", 0, 1)]
    [Arguments("@abc", 0, 1)]
    [Arguments("\"\"\"raw\"\"\"", 0, 1)]
    [Arguments("\"a\"", 4, 1)]
    [Arguments("\"a\"", 0, 4)]
    public async Task UnmappableSpanReturnsFalseAsync(string text, int start, int length)
    {
        var literal = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(text, string.Empty));
        await Assert.That(StringLiteralSpanMapper.TryMap(literal, start, length, out var span)).IsFalse();
        await Assert.That(span.Length).IsEqualTo(0);
    }
}
