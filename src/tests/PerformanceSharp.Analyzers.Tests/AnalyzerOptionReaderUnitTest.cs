// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Diagnostics;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>
/// Direct tests for <see cref="AnalyzerOptionReader"/>, the editorconfig readers shared by the
/// option records (PSH1007, PSH1017, PSH1411).
/// </summary>
public class AnalyzerOptionReaderUnitTest
{
    /// <summary>A rule-specific key used by the tests.</summary>
    private const string RuleKey = "performancesharp.PSH0000.example";

    /// <summary>A project-wide key used by the tests.</summary>
    private const string GeneralKey = "performancesharp.example";

    /// <summary>Verifies the list is trimmed and empty entries are dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadCommaSeparatedListTrimsAndDropsEmptyEntriesAsync()
    {
        var options = Options((RuleKey, "A, B ,, C , "));

        var parsed = AnalyzerOptionReader.ReadCommaSeparatedList(options, RuleKey, GeneralKey);

        await Assert.That(parsed).IsEquivalentTo(["A", "B", "C"]);
    }

    /// <summary>Verifies the rule-specific key wins over the project-wide key.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadCommaSeparatedListPrefersTheRuleKeyAsync()
    {
        var options = Options((RuleKey, "rule"), (GeneralKey, "general"));

        var parsed = AnalyzerOptionReader.ReadCommaSeparatedList(options, RuleKey, GeneralKey);

        await Assert.That(parsed).IsEquivalentTo(["rule"]);
    }

    /// <summary>Verifies the project-wide key is used when the rule key is absent, and empty is the fallback.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadCommaSeparatedListFallsBackToGeneralThenEmptyAsync()
    {
        await Assert.That(AnalyzerOptionReader.ReadCommaSeparatedList(Options((GeneralKey, "only")), RuleKey, GeneralKey))
            .IsEquivalentTo(["only"]);
        await Assert.That(AnalyzerOptionReader.ReadCommaSeparatedList(Options(), RuleKey, GeneralKey).Length).IsEqualTo(0);
    }

    /// <summary>Verifies boolean reads honour precedence, parsing, and the false default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadBoolHonoursPrecedenceAndDefaultsToFalseAsync()
    {
        await Assert.That(AnalyzerOptionReader.ReadBool(Options((RuleKey, "true")), RuleKey, GeneralKey)).IsTrue();
        await Assert.That(AnalyzerOptionReader.ReadBool(Options((RuleKey, "false"), (GeneralKey, "true")), RuleKey, GeneralKey)).IsFalse();
        await Assert.That(AnalyzerOptionReader.ReadBool(Options((GeneralKey, "true")), RuleKey, GeneralKey)).IsTrue();
        await Assert.That(AnalyzerOptionReader.ReadBool(Options(), RuleKey, GeneralKey)).IsFalse();
    }

    /// <summary>Verifies an unparseable rule value falls through to the project-wide key.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadBoolFallsThroughAnUnparseableRuleValueAsync()
    {
        await Assert.That(AnalyzerOptionReader.ReadBool(Options((RuleKey, "maybe"), (GeneralKey, "true")), RuleKey, GeneralKey)).IsTrue();
        await Assert.That(AnalyzerOptionReader.ReadBool(Options((RuleKey, "maybe")), RuleKey, GeneralKey)).IsFalse();
    }

    /// <summary>Builds a fake options bag from key/value pairs.</summary>
    /// <param name="entries">The configured entries.</param>
    /// <returns>The options bag.</returns>
    private static FakeConfigOptions Options(params (string Key, string Value)[] entries)
    {
        var values = new Dictionary<string, string>(entries.Length);
        foreach (var (key, value) in entries)
        {
            values[key] = value;
        }

        return new FakeConfigOptions(values);
    }

    /// <summary>An in-memory <see cref="AnalyzerConfigOptions"/> backed by a dictionary.</summary>
    private sealed class FakeConfigOptions : AnalyzerConfigOptions
    {
        /// <summary>The configured key/value pairs.</summary>
        private readonly Dictionary<string, string> _values;

        /// <summary>Initializes a new instance of the <see cref="FakeConfigOptions"/> class.</summary>
        /// <param name="values">The backing values.</param>
        public FakeConfigOptions(Dictionary<string, string> values) => _values = values;

        /// <inheritdoc/>
        public override bool TryGetValue(string key, out string value)
        {
            if (_values.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }

            value = null!;
            return false;
        }
    }
}
