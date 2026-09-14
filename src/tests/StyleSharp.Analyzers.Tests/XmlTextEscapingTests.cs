// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests escaping documentation-comment character data.</summary>
public class XmlTextEscapingTests
{
    /// <summary>Verifies the markup characters are replaced by their entities and everything else is kept.</summary>
    /// <param name="text">The text to escape.</param>
    /// <param name="expected">The escaped text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("List<int>", "List&lt;int&gt;")]
    [Arguments("a & b", "a &amp; b")]
    [Arguments("&lt;", "&amp;lt;")]
    [Arguments("plain text", "plain text")]
    [Arguments("", "")]
    public async Task EscapesMarkupCharactersAsync(string text, string expected)
    {
        await Assert.That(XmlTextEscaping.Escape(text)).IsEqualTo(expected);
        await Assert.That(XmlTextEscaping.Escape(text.AsSpan())).IsEqualTo(expected);
    }

    /// <summary>Verifies a string with nothing to escape comes back as the same instance.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnchangedStringIsReturnedAsIsAsync()
    {
        const string Text = "IEnumerable";

        await Assert.That(XmlTextEscaping.Escape(Text)).IsSameReferenceAs(Text);
    }
}
