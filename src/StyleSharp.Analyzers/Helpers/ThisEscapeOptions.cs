// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST2403 settings for one syntax tree.</summary>
/// <param name="AllowedEscapeMethods">The methods that may be handed the instance during construction.</param>
internal readonly record struct ThisEscapeOptions(string[] AllowedEscapeMethods)
{
    /// <summary>The rule-specific allowed-receiver key.</summary>
    private const string AllowedEscapeMethodsRuleKey = "stylesharp.SST2403.allowed_escape_methods";

    /// <summary>The project-wide allowed-receiver key.</summary>
    private const string AllowedEscapeMethodsGeneralKey = "stylesharp.allowed_escape_methods";

    /// <summary>Reads the settings for one tree.</summary>
    /// <param name="options">The analyzer config options for the constructor's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// Whether a callee is safe to hand a half-built object to is a fact about that callee, and nothing
    /// in the source says it. A framework that takes the instance only to notify it later — after
    /// construction, on activation — is indistinguishable from one that publishes it immediately, so the
    /// codebase names the ones it trusts rather than the rule guessing.
    /// </remarks>
    public static ThisEscapeOptions Read(AnalyzerConfigOptions options)
        => new(AnalyzerOptionReader.ReadCommaSeparatedList(options, AllowedEscapeMethodsRuleKey, AllowedEscapeMethodsGeneralKey));

    /// <summary>Returns whether a method has been named as safe to receive the instance.</summary>
    /// <param name="methodName">The simple name of the invoked method.</param>
    /// <returns><see langword="true"/> when the method is on the configured list.</returns>
    public bool Allows(string methodName)
    {
        for (var i = 0; i < AllowedEscapeMethods.Length; i++)
        {
            if (string.Equals(AllowedEscapeMethods[i], methodName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
