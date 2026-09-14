// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared editorconfig readers' key precedence, parsing and fallbacks.</summary>
public sealed class AnalyzerOptionReaderUnitTest
{
    /// <summary>A rule-specific key used by the tests.</summary>
    private const string RuleKey = "stylesharp.SST0000.example";

    /// <summary>A project-wide key used by the tests.</summary>
    private const string GeneralKey = "stylesharp.example";

    /// <summary>The value configured under the rule-specific key.</summary>
    private const string RuleValue = "rule";

    /// <summary>The value configured under the project-wide key.</summary>
    private const string GeneralValue = "general";

    /// <summary>The fallback the positive-integer read is given.</summary>
    private const int PositiveFallback = 7;

    /// <summary>Verifies the raw value of the first set key is returned, preferring the rule key.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetValuePrefersTheRuleKeyAsync()
    {
        var both = new DictionaryConfigOptions(new() { [RuleKey] = RuleValue, [GeneralKey] = GeneralValue });
        var generalOnly = new DictionaryConfigOptions(new() { [GeneralKey] = GeneralValue });

        await Assert.That(AnalyzerOptionReader.TryGetValue(both, RuleKey, GeneralKey, out var fromBoth)).IsTrue();
        await Assert.That(fromBoth).IsEqualTo(RuleValue);
        await Assert.That(AnalyzerOptionReader.TryGetValue(generalOnly, RuleKey, GeneralKey, out var fromGeneral)).IsTrue();
        await Assert.That(fromGeneral).IsEqualTo(GeneralValue);
        await Assert.That(AnalyzerOptionReader.TryGetValue(new DictionaryConfigOptions(new()), RuleKey, GeneralKey, out _)).IsFalse();
    }

    /// <summary>Verifies the first-set boolean is on only for a true first key, and an unparsable rule value does not fall through.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadFirstSetBoolStopsAtTheFirstSetKeyAsync()
    {
        await Assert.That(AnalyzerOptionReader.ReadFirstSetBool(new DictionaryConfigOptions(new() { [RuleKey] = "true" }), RuleKey, GeneralKey)).IsTrue();
        await Assert.That(AnalyzerOptionReader.ReadFirstSetBool(new DictionaryConfigOptions(new() { [GeneralKey] = "true" }), RuleKey, GeneralKey)).IsTrue();
        await Assert.That(AnalyzerOptionReader.ReadFirstSetBool(new DictionaryConfigOptions(new() { [RuleKey] = "maybe", [GeneralKey] = "true" }), RuleKey, GeneralKey)).IsFalse();
        await Assert.That(AnalyzerOptionReader.ReadFirstSetBool(new DictionaryConfigOptions(new()), RuleKey, GeneralKey)).IsFalse();
    }

    /// <summary>Verifies the boolean read with a fallback uses it only when neither key parses.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadBoolUsesTheFallbackOnlyWhenNothingParsesAsync()
    {
        await Assert.That(AnalyzerOptionReader.ReadBool(new DictionaryConfigOptions(new()), RuleKey, GeneralKey, fallback: true)).IsTrue();
        await Assert.That(AnalyzerOptionReader.ReadBool(new DictionaryConfigOptions(new() { [RuleKey] = "false" }), RuleKey, GeneralKey, fallback: true)).IsFalse();
        await Assert.That(AnalyzerOptionReader.ReadBool(new DictionaryConfigOptions(new() { [RuleKey] = "maybe", [GeneralKey] = "false" }), RuleKey, GeneralKey, fallback: true)).IsFalse();
    }

    /// <summary>Verifies only a positive integer is read, with the general key and then the fallback behind the rule key.</summary>
    /// <param name="ruleValue">The rule-specific value, or an empty string for unset.</param>
    /// <param name="generalValue">The project-wide value, or an empty string for unset.</param>
    /// <param name="expected">The expected result.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("5", "9", 5)]
    [Arguments("", "9", 9)]
    [Arguments("0", "9", 9)]
    [Arguments("-3", "", PositiveFallback)]
    [Arguments("five", "", PositiveFallback)]
    [Arguments("", "", PositiveFallback)]
    public async Task ReadPositiveIntAsync(string ruleValue, string generalValue, int expected)
    {
        var values = new Dictionary<string, string>();
        if (ruleValue.Length != 0)
        {
            values[RuleKey] = ruleValue;
        }

        if (generalValue.Length != 0)
        {
            values[GeneralKey] = generalValue;
        }

        await Assert.That(AnalyzerOptionReader.ReadPositiveInt(new DictionaryConfigOptions(values), RuleKey, GeneralKey, PositiveFallback)).IsEqualTo(expected);
    }

    /// <summary>Verifies a segment is trimmed on both ends within its bounds.</summary>
    /// <param name="value">The option text.</param>
    /// <param name="start">The inclusive start.</param>
    /// <param name="end">The exclusive end.</param>
    /// <param name="expected">The trimmed segment.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("  abc  ", 0, 7, "abc")]
    [Arguments("x, y ,z", 2, 5, "y")]
    [Arguments("   ", 0, 3, "")]
    public async Task TrimSegmentAsync(string value, int start, int end, string expected)
    {
        var trimmed = AnalyzerOptionReader.TrimSegment(value, start, end).ToString();

        await Assert.That(trimmed).IsEqualTo(expected);
    }

    /// <summary>An in-memory <see cref="AnalyzerConfigOptions"/> backed by a dictionary.</summary>
    /// <param name="values">The configured key and value pairs.</param>
    private sealed class DictionaryConfigOptions(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        /// <inheritdoc/>
        public override bool TryGetValue(string key, out string value)
        {
            if (values.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }

            value = null!;
            return false;
        }
    }
}
