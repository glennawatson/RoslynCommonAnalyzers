// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests configured log floors and the bounds of reportable levels.</summary>
public class LogLevelFloorOptionsTests
{
    /// <summary>Verifies known names, whitespace, and invalid values resolve the expected floor.</summary>
    /// <param name="value">The configured name, or null when the key is absent.</param>
    /// <param name="expected">The resolved ordinal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(null, 4)]
    [Arguments("trace", 0)]
    [Arguments("DEBUG", 1)]
    [Arguments("Information", 2)]
    [Arguments("warning", 3)]
    [Arguments("error", 4)]
    [Arguments("critical", 5)]
    [Arguments(" \tDeBuG\r\n", 1)]
    [Arguments("", 4)]
    [Arguments(" \t\r\n", 4)]
    [Arguments("verbose", 4)]
    [Arguments("none", 4)]
    [Arguments("2", 4)]
    public async Task ConfiguredFloorIsResolvedAsync(string? value, int expected)
    {
        var options = LogLevelFloorOptions.Read(new LevelOptions(value));
        await Assert.That(options.Floor).IsEqualTo(expected);
        await Assert.That(options.Includes(expected - 1)).IsFalse();
        await Assert.That(options.Includes(expected)).IsTrue();
        await Assert.That(options.Includes(LogLevelFloorOptions.Critical)).IsTrue();
        await Assert.That(options.Includes(LogLevelFloorOptions.Critical + 1)).IsFalse();
    }

    /// <summary>Supplies the rule's optional raw configuration value.</summary>
    /// <param name="configuredValue">The configured value, or null for an absent key.</param>
    private sealed class LevelOptions(string? configuredValue) : AnalyzerConfigOptions
    {
        /// <inheritdoc/>
        public override bool TryGetValue(string key, out string value)
        {
            value = configuredValue ?? string.Empty;
            return key == "stylesharp.SST2438.minimum_level" && configuredValue is not null;
        }
    }
}
