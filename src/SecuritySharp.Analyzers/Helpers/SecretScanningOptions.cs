// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>Reads the settings that widen what the secret scanning rules accept.</summary>
internal static class SecretScanningOptions
{
    /// <summary>The rule-specific key that accepts any key carrying a published-sample marker.</summary>
    public const string AllowDocumentationExamplesRuleKey = "securitysharp.SES1201.allow_documentation_examples";

    /// <summary>The project-wide key that accepts any key carrying a published-sample marker.</summary>
    public const string AllowDocumentationExamplesGeneralKey = "securitysharp.allow_documentation_examples";

    /// <summary>The rule-specific key naming the exact sample values to accept.</summary>
    public const string AllowedExamplesRuleKey = "securitysharp.SES1201.allowed_example_secrets";

    /// <summary>The project-wide key naming the exact sample values to accept.</summary>
    public const string AllowedExamplesGeneralKey = "securitysharp.allowed_example_secrets";

    /// <summary>Reads the secret-scanning settings for one tree.</summary>
    /// <param name="options">The analyzer config options for the literal's tree.</param>
    /// <returns>The resolved settings; the default value reports every recognised shape.</returns>
    /// <remarks>
    /// Both settings are off by default. A named list is the narrower of the two and says exactly which
    /// literals a project vouches for; the marker switch trusts a convention a live credential can also
    /// satisfy, so it stays a deliberate choice.
    /// </remarks>
    public static SecretScanningSettings Read(AnalyzerConfigOptions options)
        => new(ReadAllowDocumentationExamples(options), ReadAllowedExamples(options));

    /// <summary>Reads whether any key carrying a published-sample marker is accepted.</summary>
    /// <param name="options">The analyzer config options for the literal's tree.</param>
    /// <returns><see langword="true"/> only when the option is set and parses as true.</returns>
    private static bool ReadAllowDocumentationExamples(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(AllowDocumentationExamplesRuleKey, out var value)
            && !options.TryGetValue(AllowDocumentationExamplesGeneralKey, out value))
        {
            return false;
        }

        return bool.TryParse(value.Trim(), out var parsed) && parsed;
    }

    /// <summary>Reads the exact sample values a project accepts.</summary>
    /// <param name="options">The analyzer config options for the literal's tree.</param>
    /// <returns>The configured values, or <see langword="null"/> when none are named.</returns>
    /// <remarks>
    /// A credential can itself contain a comma, so an entry is taken whole and only its surrounding
    /// whitespace is trimmed. An empty entry is dropped so a stray separator vouches for nothing.
    /// </remarks>
    private static string[]? ReadAllowedExamples(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(AllowedExamplesRuleKey, out var value)
            && !options.TryGetValue(AllowedExamplesGeneralKey, out value))
        {
            return null;
        }

        var parts = value.Split(',');
        var kept = new string[parts.Length];
        var count = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            var entry = parts[i].Trim();
            if (entry.Length > 0)
            {
                kept[count++] = entry;
            }
        }

        if (count == 0)
        {
            return null;
        }

        if (count == kept.Length)
        {
            return kept;
        }

        var trimmed = new string[count];
        Array.Copy(kept, trimmed, count);
        return trimmed;
    }
}
