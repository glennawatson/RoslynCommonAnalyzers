// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests quoting and escaping in custom date and time formats.</summary>
public sealed class DateFormatTextTests
{
    /// <summary>Separators are active only outside quoted or escaped literal text.</summary>
    /// <param name="format">The custom format.</param>
    /// <param name="expected">Whether a culture-sensitive separator remains.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", false)]
    [Arguments("yyyyMMdd", false)]
    [Arguments("dd/MM", true)]
    [Arguments("HH:mm", true)]
    [Arguments("'/:text'", false)]
    [Arguments("\"/:text\"", false)]
    [Arguments("'\"/:'", false)]
    [Arguments("\"'/ :\"", false)]
    [Arguments("'/'/", true)]
    [Arguments("\":\":", true)]
    [Arguments("dd\\/MM\\:ss", false)]
    [Arguments("\\/:", true)]
    [Arguments("'a\\'/:b'", false)]
    [Arguments("'unclosed/:", false)]
    [Arguments("yyyy\\", false)]
    public async Task HasUnquotedSeparatorHonorsLiteralsAsync(string format, bool expected)
    {
        var actual = DateFormatText.HasUnquotedSeparator(format);
        await Assert.That(actual).IsEqualTo(expected);
    }

    /// <summary>Quoting preserves literal text, including escaped quotes and an incomplete trailing escape.</summary>
    /// <param name="format">The original format.</param>
    /// <param name="expected">The format with only active separators quoted.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "")]
    [Arguments("yyyyMMdd", "yyyyMMdd")]
    [Arguments("dd/MM HH:mm", "dd'/'MM HH':'mm")]
    [Arguments("'/:text'", "'/:text'")]
    [Arguments("\"/:text\"", "\"/:text\"")]
    [Arguments("'\"/:'", "'\"/:'")]
    [Arguments("\"'/ :\"", "\"'/ :\"")]
    [Arguments("'/'/", "'/''/'")]
    [Arguments("\":\":", "\":\"':'")]
    [Arguments("dd\\/MM\\:ss", "dd\\/MM\\:ss")]
    [Arguments("\\/:/", "\\/':''/'")]
    [Arguments("'a\\'/:b'", "'a\\'/:b'")]
    [Arguments("'unclosed/:", "'unclosed/:")]
    [Arguments("yyyy\\", "yyyy\\")]
    public async Task QuoteSeparatorsPreservesLiteralsAsync(string format, string expected)
    {
        var actual = DateFormatText.QuoteSeparators(format);
        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(DateFormatText.HasUnquotedSeparator(actual)).IsFalse();
    }
}
