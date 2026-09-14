// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>
/// Builds the diagnostic descriptors every package ships, each with a docs-page help link derived from
/// the rule id. Each Rules class wraps these with its category so a descriptor declaration stays a single
/// call and the construction shape lives in one place.
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static DiagnosticDescriptor Create(string id, LocalizableString title, LocalizableString messageFormat, string category, LocalizableString description) =>
        Build(id, title, messageFormat, category, DiagnosticSeverity.Warning, true, description);

    /// <summary>Creates a Warning descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="category">The rule category.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static DiagnosticDescriptor CreateOptIn(string id, LocalizableString title, LocalizableString messageFormat, string category, LocalizableString description) =>
        Build(id, title, messageFormat, category, DiagnosticSeverity.Warning, false, description);

    /// <summary>Creates an enabled-by-default Info descriptor, for a nudge where the code still compiles and runs correctly.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="category">The rule category.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static DiagnosticDescriptor CreateInfo(string id, LocalizableString title, LocalizableString messageFormat, string category, LocalizableString description) =>
        Build(id, title, messageFormat, category, DiagnosticSeverity.Info, true, description);

    /// <summary>Builds the docs-page help link for a rule id.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <returns>The help link.</returns>
    private static string BuildHelpLink(string id) =>
        $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md";

    /// <summary>Builds a descriptor with the docs-page help link for its id.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="category">The rule category.</param>
    /// <param name="severity">The default severity.</param>
    /// <param name="isEnabledByDefault">Whether the rule is on without configuration.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Build(
        string id,
        LocalizableString title,
        LocalizableString messageFormat,
        string category,
        DiagnosticSeverity severity,
        bool isEnabledByDefault,
        LocalizableString description) =>
        new(id, title, messageFormat, category, severity, isEnabledByDefault, description, BuildHelpLink(id));
}
