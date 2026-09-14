// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Tests the classification of a string literal under the secret-scanning settings of its tree.</summary>
public class HardcodedSecretLiteralClassificationTests
{
    /// <summary>The published sample access key id, assembled from parts so no single literal carries the secret shape.</summary>
    private const string SampleKey = "AKIA" + "IOSFODNN7EXAMPLE";

    /// <summary>Verifies a literal's decoded text is classified with no settings configured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralIsClassifiedWithDefaultSettingsAsync() =>
        await Assert.That(HardcodedSecretClassifier.ClassifyLiteral(ParseLiteral(), Options(null, string.Empty)))
            .IsEqualTo(HardcodedSecretClassifier.AwsAccessKeyId);

    /// <summary>Verifies the settings read from the literal's tree are applied.</summary>
    /// <param name="key">The configured option key.</param>
    /// <param name="value">The configured option value.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(SecretScanningOptions.AllowDocumentationExamplesGeneralKey, "true")]
    [Arguments(SecretScanningOptions.AllowedExamplesRuleKey, SampleKey)]
    public async Task LiteralIsClassifiedWithItsTreeSettingsAsync(string key, string value) =>
        await Assert.That(HardcodedSecretClassifier.ClassifyLiteral(ParseLiteral(), Options(key, value))).IsNull();

    /// <summary>Parses the sample key as a string literal.</summary>
    /// <returns>The literal expression.</returns>
    private static LiteralExpressionSyntax ParseLiteral() =>
        (LiteralExpressionSyntax)SyntaxFactory.ParseExpression($"\"{SampleKey}\"");

    /// <summary>Builds analyzer options whose every tree sees at most one configured entry.</summary>
    /// <param name="key">The configured key, or <see langword="null"/> for no entry.</param>
    /// <param name="value">The configured value.</param>
    /// <returns>The analyzer options.</returns>
    private static AnalyzerOptions Options(string? key, string value) =>
        new(ImmutableArray<AdditionalText>.Empty, new SingleEntryOptionsProvider(new SingleEntryOptions(key, value)));

    /// <summary>An options provider handing every tree the same options.</summary>
    /// <param name="options">The options every lookup returns.</param>
    private sealed class SingleEntryOptionsProvider(AnalyzerConfigOptions options) : AnalyzerConfigOptionsProvider
    {
        /// <inheritdoc/>
        public override AnalyzerConfigOptions GlobalOptions => options;

        /// <inheritdoc/>
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => options;

        /// <inheritdoc/>
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
    }

    /// <summary>In-memory options holding at most one key.</summary>
    /// <param name="configuredKey">The configured key, or <see langword="null"/> for no entry.</param>
    /// <param name="configuredValue">The configured value.</param>
    private sealed class SingleEntryOptions(string? configuredKey, string configuredValue) : AnalyzerConfigOptions
    {
        /// <inheritdoc/>
        public override bool TryGetValue(string key, out string value)
        {
            var found = string.Equals(configuredKey, key, StringComparison.Ordinal);
            value = found ? configuredValue : null!;
            return found;
        }
    }
}
