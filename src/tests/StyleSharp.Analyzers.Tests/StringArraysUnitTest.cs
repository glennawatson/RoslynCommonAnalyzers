// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared ordinal lookup over fixed name tables.</summary>
public sealed class StringArraysUnitTest
{
    /// <summary>The table the lookups search.</summary>
    private static readonly string[] Names = ["Redirect", "RedirectPermanent"];

    /// <summary>Verifies an exact entry is found.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsOrdinalFindsAnExactEntryAsync() =>
        await Assert.That(StringArrays.ContainsOrdinal(Names, "RedirectPermanent")).IsTrue();

    /// <summary>Verifies the comparison is case-sensitive and whole-string.</summary>
    /// <param name="value">A value that differs from every entry.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("redirect")]
    [Arguments("Redirec")]
    [Arguments("")]
    public async Task ContainsOrdinalRejectsNearMissesAsync(string value) =>
        await Assert.That(StringArrays.ContainsOrdinal(Names, value)).IsFalse();

    /// <summary>Verifies an empty table contains nothing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsOrdinalOnAnEmptyTableIsFalseAsync() =>
        await Assert.That(StringArrays.ContainsOrdinal([], "Redirect")).IsFalse();
}
