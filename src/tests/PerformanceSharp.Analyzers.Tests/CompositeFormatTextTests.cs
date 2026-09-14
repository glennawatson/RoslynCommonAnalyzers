// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the conservative composite-format grammar used before hoisting a format string.</summary>
public class CompositeFormatTextTests
{
    /// <summary>Verifies ordinary placeholders, alignment, format specifiers, and escaped braces are accepted.</summary>
    /// <param name="text">The format containing at least one complete placeholder.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("{0}")]
    [Arguments("before {12} after {0}")]
    [Arguments("{{{0}}}")]
    [Arguments("{0,8}")]
    [Arguments("{0,-8}")]
    [Arguments("{0 , 8 }")]
    [Arguments("{0 , -8 :X2}")]
    [Arguments("{0:}")]
    [Arguments("{0:yyyy-MM-dd}")]
    public async Task CompletePlaceholderIsAcceptedAsync(string text)
    {
        var result = CompositeFormatText.IsWellFormed(text);

        await Assert.That(result).IsTrue();
        await Assert.That(System.Text.CompositeFormat.Parse(text)).IsNotNull();
    }

    /// <summary>Verifies malformed clauses and strings without placeholders are refused before hoisting.</summary>
    /// <param name="text">The incomplete or unsupported format.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("plain text")]
    [Arguments("{{escaped}}")]
    [Arguments("}")]
    [Arguments("}x")]
    [Arguments("{")]
    [Arguments("{}")]
    [Arguments("{x}")]
    [Arguments("{-1}")]
    [Arguments("{0")]
    [Arguments("{0 ")]
    [Arguments("{0x}")]
    [Arguments("{0,")]
    [Arguments("{0, ")]
    [Arguments("{0,-")]
    [Arguments("{0,+8}")]
    [Arguments("{0,a}")]
    [Arguments("{0,-}")]
    [Arguments("{0,8")]
    [Arguments("{0,8 ")]
    [Arguments("{0,8x}")]
    [Arguments("{0:")]
    [Arguments("{0:X")]
    [Arguments("{0:{x}}")]
    [Arguments("{0:{{}}}")]
    [Arguments("{0} trailing }")]
    public async Task MissingOrMalformedPlaceholderIsRejectedAsync(string text)
    {
        var result = CompositeFormatText.IsWellFormed(text);

        await Assert.That(result).IsFalse();
    }
}
