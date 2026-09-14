// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Tests the region comparisons against lower-case keywords.</summary>
public class AsciiTextTests
{
    /// <summary>The words the any-of comparison matches against.</summary>
    private static readonly string[] Keys = ["server", "host"];

    /// <summary>Verifies a region matches its lower-case word with ASCII letters folded and nothing else.</summary>
    /// <param name="value">The text.</param>
    /// <param name="start">The inclusive region start.</param>
    /// <param name="end">The exclusive region end.</param>
    /// <param name="word">The lower-case word.</param>
    /// <param name="expected">Whether the region equals the word.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("xxPasswordyy", 2, 10, "password", true)]
    [Arguments("PASSWORD", 0, 8, "password", true)]
    [Arguments("password", 0, 8, "password", true)]
    [Arguments("passwords", 0, 9, "password", false)]
    [Arguments("passw0rd", 0, 8, "password", false)]
    [Arguments("Pässword", 0, 8, "pässword", true)]
    [Arguments("PÄSSWORD", 0, 8, "pässword", false)]
    [Arguments("", 0, 0, "", true)]
    public async Task RegionMatchesTheLowercaseWordAsync(string value, int start, int end, string word, bool expected) =>
        await Assert.That(AsciiText.RegionEqualsLowercase(value, start, end, word)).IsEqualTo(expected);

    /// <summary>Verifies a region matches when it equals any of the words.</summary>
    /// <param name="value">The whole text, used as the region.</param>
    /// <param name="expected">Whether the text equals one of the keys.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("HOST", true)]
    [Arguments("Server", true)]
    [Arguments("hosts", false)]
    [Arguments("", false)]
    public async Task RegionMatchesAnyWordAsync(string value, bool expected) =>
        await Assert.That(AsciiText.RegionEqualsAnyLowercase(value, 0, value.Length, Keys)).IsEqualTo(expected);

    /// <summary>Verifies an empty word set never matches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyWordSetNeverMatchesAsync() =>
        await Assert.That(AsciiText.RegionEqualsAnyLowercase(Keys[1], 0, Keys[1].Length, [])).IsFalse();
}
