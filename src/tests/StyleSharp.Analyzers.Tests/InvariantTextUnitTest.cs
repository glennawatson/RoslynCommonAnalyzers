// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared culture-invariant lowercase comparison.</summary>
public sealed class InvariantTextUnitTest
{
    /// <summary>Verifies text matching the word in any casing is equal.</summary>
    /// <param name="text">The text to compare.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("block_scoped")]
    [Arguments("Block_Scoped")]
    [Arguments("BLOCK_SCOPED")]
    public async Task TextInAnyCasingEqualsTheWordAsync(string text)
    {
        var equal = InvariantText.EqualsLowercase(text, "block_scoped");

        await Assert.That(equal).IsTrue();
    }

    /// <summary>Verifies a different length or a different character is not equal.</summary>
    /// <param name="text">The text to compare.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("block")]
    [Arguments("block_scopes")]
    [Arguments("")]
    public async Task DifferentTextIsNotEqualAsync(string text)
    {
        var equal = InvariantText.EqualsLowercase(text, "block_scoped");

        await Assert.That(equal).IsFalse();
    }
}
