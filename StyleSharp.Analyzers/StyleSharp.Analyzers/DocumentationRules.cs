// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Single source of truth for the documentation (SST16xx) diagnostic descriptors.
/// </summary>
internal static class DocumentationRules
{
    /// <summary>
    /// SST1653 — a <c>&lt;summary&gt;</c> whose text fits within the configured
    /// length should be written on a single line. StyleSharp-original (no StyleCop
    /// equivalent); the length is set with <c>stylesharp.summary_single_line_max_length</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor SingleLineSummary = Create(
        "SST1653",
        "Short documentation summaries should be on a single line",
        "This summary is short enough to fit on a single line",
        "A '<summary>' whose combined text is shorter than the configured limit (default 100 characters) should be written on one line: '/// <summary>Text</summary>'.");

    /// <summary>Creates a Warning-severity Documentation descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Documentation",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
