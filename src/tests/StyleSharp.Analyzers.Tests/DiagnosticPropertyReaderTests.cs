// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests reading the values an analyzer stashes in a diagnostic's properties.</summary>
public class DiagnosticPropertyReaderTests
{
    /// <summary>The property key the tests write.</summary>
    private const string Key = "Index";

    /// <summary>Verifies an integer property parses and anything else reports failure with -1.</summary>
    /// <param name="text">The stored text, or <see langword="null"/> to leave the property out.</param>
    /// <param name="expectedFound">Whether the read succeeds.</param>
    /// <param name="expectedValue">The value read.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("3", true, 3)]
    [Arguments("-2", true, -2)]
    [Arguments("x", false, 0)]
    [Arguments(null, false, -1)]
    public async Task ReadsInvariantIntegersAsync(string? text, bool expectedFound, int expectedValue)
    {
        var properties = text is null ? ImmutableDictionary<string, string?>.Empty : ImmutableDictionary<string, string?>.Empty.Add(Key, text);
        var diagnostic = Diagnostic.Create(CorrectnessRules.NonShortCircuitGuard, Location.None, properties);

        var found = DiagnosticPropertyReader.TryGetInt32(diagnostic, Key, out var value);

        await Assert.That(found).IsEqualTo(expectedFound);
        await Assert.That(value).IsEqualTo(expectedValue);
    }
}
