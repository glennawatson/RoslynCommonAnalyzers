// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis.Diagnostics;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Tests how secret scanning resolves configured documentation samples.</summary>
public class SecretScanningOptionsTests
{
    /// <summary>The exact sample value shared by the option fixtures.</summary>
    private const string SampleValue = "alpha";

    /// <summary>Verifies missing options do not exempt documentation markers or named samples.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingOptionsKeepBothExemptionsDisabledAsync()
    {
        var settings = SecretScanningOptions.Read(Options());

        await Assert.That(settings.AllowDocumentationExamples).IsFalse();
        await Assert.That(settings.AllowedExamples).IsNull();
    }

    /// <summary>Verifies either option key accepts only a successfully parsed true value.</summary>
    /// <param name="key">The rule-specific or project-wide option key.</param>
    /// <param name="value">The configured boolean text.</param>
    /// <param name="expected">Whether documentation markers should be accepted.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(SecretScanningOptions.AllowDocumentationExamplesRuleKey, "true", true)]
    [Arguments(SecretScanningOptions.AllowDocumentationExamplesRuleKey, "false", false)]
    [Arguments(SecretScanningOptions.AllowDocumentationExamplesRuleKey, "invalid", false)]
    [Arguments(SecretScanningOptions.AllowDocumentationExamplesRuleKey, "", false)]
    [Arguments(SecretScanningOptions.AllowDocumentationExamplesGeneralKey, "true", true)]
    [Arguments(SecretScanningOptions.AllowDocumentationExamplesGeneralKey, "false", false)]
    [Arguments(SecretScanningOptions.AllowDocumentationExamplesGeneralKey, "invalid", false)]
    [Arguments(SecretScanningOptions.AllowDocumentationExamplesGeneralKey, " TrUe ", true)]
    public async Task DocumentationOptionRequiresTrueAsync(string key, string value, bool expected)
    {
        var settings = SecretScanningOptions.Read(Options((key, value)));

        await Assert.That(settings.AllowDocumentationExamples).IsEqualTo(expected);
    }

    /// <summary>Verifies a present rule-specific value takes precedence even when it cannot be parsed.</summary>
    /// <param name="ruleValue">The rule-specific boolean text.</param>
    /// <param name="generalValue">The project-wide boolean text.</param>
    /// <param name="expected">Whether documentation markers should be accepted.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("true", "false", true)]
    [Arguments("false", "true", false)]
    [Arguments("invalid", "true", false)]
    [Arguments("", "true", false)]
    public async Task DocumentationRuleOptionOverridesGeneralOptionAsync(string ruleValue, string generalValue, bool expected)
    {
        var settings = SecretScanningOptions.Read(Options(
            (SecretScanningOptions.AllowDocumentationExamplesRuleKey, ruleValue),
            (SecretScanningOptions.AllowDocumentationExamplesGeneralKey, generalValue)));

        await Assert.That(settings.AllowDocumentationExamples).IsEqualTo(expected);
    }

    /// <summary>Verifies either list option can name an exact sample independently of the marker option.</summary>
    /// <param name="key">The rule-specific or project-wide list key.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(SecretScanningOptions.AllowedExamplesRuleKey)]
    [Arguments(SecretScanningOptions.AllowedExamplesGeneralKey)]
    public async Task NamedSampleCanBeConfiguredWithEitherKeyAsync(string key)
    {
        var settings = SecretScanningOptions.Read(Options((key, SampleValue)));

        await Assert.That(settings.AllowDocumentationExamples).IsFalse();
        await Assert.That(settings.AllowedExamples).IsEquivalentTo([SampleValue]);
    }

    /// <summary>Verifies a rule-specific list replaces the project-wide list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NamedSamplesRuleOptionOverridesGeneralOptionAsync()
    {
        var settings = SecretScanningOptions.Read(Options(
            (SecretScanningOptions.AllowedExamplesRuleKey, "rule sample"),
            (SecretScanningOptions.AllowedExamplesGeneralKey, "general sample")));

        await Assert.That(settings.AllowedExamples).IsEquivalentTo(["rule sample"]);
    }

    /// <summary>Verifies an explicitly empty rule list disables project-wide sample exemptions.</summary>
    /// <param name="value">The configured list containing no nonempty entries.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments(",")]
    [Arguments(",,")]
    [Arguments(" \t\u2003 ")]
    [Arguments(" , \t, ")]
    public async Task EmptyRuleListDoesNotFallBackToGeneralListAsync(string value)
    {
        var settings = SecretScanningOptions.Read(Options(
            (SecretScanningOptions.AllowedExamplesRuleKey, value),
            (SecretScanningOptions.AllowedExamplesGeneralKey, "general sample")));

        await Assert.That(settings.AllowedExamples).IsNull();
    }

    /// <summary>Verifies surrounding whitespace and empty entries do not change the named sample.</summary>
    /// <param name="value">The configured list around one nonempty sample.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(SampleValue)]
    [Arguments(" alpha")]
    [Arguments("alpha ")]
    [Arguments(" \talpha\u2003 ")]
    [Arguments(",alpha")]
    [Arguments("alpha,")]
    [Arguments(",,alpha,,")]
    [Arguments(" , alpha , \t ")]
    public async Task NamedSampleIsTrimmedAndEmptyEntriesAreDroppedAsync(string value)
    {
        var settings = SecretScanningOptions.Read(Options((SecretScanningOptions.AllowedExamplesRuleKey, value)));

        await Assert.That(settings.AllowedExamples).IsEquivalentTo([SampleValue]);
    }

    /// <summary>Verifies comma-separated samples retain their order and internal whitespace.</summary>
    /// <param name="value">The configured list, with or without empty entries.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("alpha,beta gamma,delta")]
    [Arguments(" alpha , beta gamma , delta ")]
    [Arguments(",alpha,, beta gamma,delta, ")]
    public async Task MultipleNamedSamplesKeepTheirExactContentsAsync(string value)
    {
        const int ExpectedCount = 3;
        var settings = SecretScanningOptions.Read(Options((SecretScanningOptions.AllowedExamplesRuleKey, value)));
        var samples = settings.AllowedExamples ?? [];

        await Assert.That(samples.Length).IsEqualTo(ExpectedCount);
        await Assert.That(samples[0]).IsEqualTo(SampleValue);
        await Assert.That(samples[1]).IsEqualTo("beta gamma");
        await Assert.That(samples[2]).IsEqualTo("delta");
    }

    /// <summary>Verifies the marker switch and named sample list can be enabled together.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DocumentationAndNamedSampleOptionsAreIndependentAsync()
    {
        var settings = SecretScanningOptions.Read(Options(
            (SecretScanningOptions.AllowDocumentationExamplesRuleKey, "true"),
            (SecretScanningOptions.AllowedExamplesGeneralKey, SampleValue)));

        await Assert.That(settings.AllowDocumentationExamples).IsTrue();
        await Assert.That(settings.AllowedExamples).IsEquivalentTo([SampleValue]);
    }

    /// <summary>Creates an in-memory options bag using the configured entries.</summary>
    /// <param name="entries">The configured option keys and values.</param>
    /// <returns>The options bag.</returns>
    private static FakeConfigOptions Options(params (string Key, string Value)[] entries)
    {
        var values = new Dictionary<string, string>(entries.Length);
        foreach (var (key, value) in entries)
        {
            values[key] = value;
        }

        return new(values);
    }

    /// <summary>Provides per-test analyzer options without editorconfig parsing.</summary>
    private sealed class FakeConfigOptions : AnalyzerConfigOptions
    {
        /// <summary>The configured option values.</summary>
        private readonly Dictionary<string, string> _values;

        /// <summary>Initializes a new instance of the <see cref="FakeConfigOptions"/> class.</summary>
        /// <param name="values">The configured option values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FakeConfigOptions(Dictionary<string, string> values) => _values = values;

        /// <summary>Reads a configured value when its key is present.</summary>
        /// <param name="key">The requested option key.</param>
        /// <param name="value">The configured value when found.</param>
        /// <returns>Whether the key is configured.</returns>
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
