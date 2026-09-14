// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Tests the any-fragment substring search.</summary>
public class TextFragmentsTests
{
    /// <summary>The fragments every search looks for.</summary>
    private static readonly string[] Fragments = ["pass", "secret"];

    /// <summary>Verifies a fragment anywhere in the text matches under the requested comparison.</summary>
    /// <param name="text">The text to search.</param>
    /// <param name="comparison">The comparison.</param>
    /// <param name="expected">Whether a fragment occurs.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("userPassword", StringComparison.OrdinalIgnoreCase, true)]
    [Arguments("clientSecretValue", StringComparison.OrdinalIgnoreCase, true)]
    [Arguments("PASSWORD", StringComparison.Ordinal, false)]
    [Arguments("passphrase", StringComparison.Ordinal, true)]
    [Arguments("token", StringComparison.OrdinalIgnoreCase, false)]
    [Arguments("", StringComparison.OrdinalIgnoreCase, false)]
    public async Task AnyFragmentMatchesUnderTheComparisonAsync(string text, StringComparison comparison, bool expected) =>
        await Assert.That(TextFragments.ContainsAny(text, Fragments, comparison)).IsEqualTo(expected);

    /// <summary>Verifies an empty fragment set never matches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyFragmentSetNeverMatchesAsync() =>
        await Assert.That(TextFragments.ContainsAny(Fragments[0], [], StringComparison.Ordinal)).IsFalse();
}
