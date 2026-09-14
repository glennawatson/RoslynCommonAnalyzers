// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared walk over the entries of a comma-separated option value.</summary>
public sealed class CommaSeparatedEntriesUnitTest
{
    /// <summary>Verifies the buffer bound is one more than the number of commas.</summary>
    /// <param name="value">The option text.</param>
    /// <param name="expected">The expected bound.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("", 1)]
    [Arguments("a", 1)]
    [Arguments("a,b,,c", 4)]
    public async Task MaxCountIsOneMoreThanTheCommasAsync(string value, int expected) =>
        await Assert.That(CommaSeparatedEntries.MaxCount(value)).IsEqualTo(expected);

    /// <summary>Verifies entries are trimmed and empty or blank entries are skipped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EntriesAreTrimmedAndBlankOnesSkippedAsync() =>
        await Assert.That(ReadAll(" a , b,, ,c ")).IsEquivalentTo(["a", "b", "c"]);

    /// <summary>Verifies a value without commas is a single entry and a blank value has none.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleAndBlankValuesAsync()
    {
        await Assert.That(ReadAll("only")).IsEquivalentTo(["only"]);
        await Assert.That(ReadAll("  ").Count).IsEqualTo(0);
    }

    /// <summary>Walks every entry of a value into a list.</summary>
    /// <param name="value">The option text.</param>
    /// <returns>The entries, in order.</returns>
    private static List<string> ReadAll(string value)
    {
        var result = new List<string>();
        var entries = new CommaSeparatedEntries(value);
        while (entries.MoveNext())
        {
            result.Add(entries.Current.ToString());
        }

        return result;
    }
}
