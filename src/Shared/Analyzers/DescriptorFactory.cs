// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>
/// Builds the diagnostic descriptors both packages ship: Warning severity and a docs-page
/// help link derived from the rule id. Each Rules class wraps these with its category so a
/// descriptor declaration stays a single call and the construction shape lives in one place.
/// </summary>
internal static class DescriptorFactory
{
    /// <summary>Creates an enabled-by-default Warning descriptor.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="category">The rule category.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    public static DiagnosticDescriptor Create(string id, string title, string messageFormat, string category, string description) =>
        new(
            id,
            title,
            messageFormat,
            category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: BuildHelpLink(id));

    /// <summary>Creates a Warning descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="category">The rule category.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    public static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string category, string description) =>
        new(
            id,
            title,
            messageFormat,
            category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: false,
            description: description,
            helpLinkUri: BuildHelpLink(id));

    /// <summary>Builds the docs-page help link for a rule id.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <returns>The help link.</returns>
    private static string BuildHelpLink(string id)
        => $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md";
}
