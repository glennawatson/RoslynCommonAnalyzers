// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the ASP.NET Core performance rules (PSH15xx). Each prefers the modern,
/// lower-overhead ASP.NET Core pattern over a legacy or allocation-heavy one, and is gated on the
/// relevant ASP.NET Core type existing in the referenced framework so a non-web project pays nothing.
/// </summary>
internal static partial class AspNetCoreRules
{
    /// <summary>Creates an enabled-by-default Warning descriptor in the AspNetCore category.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "AspNetCore", description);

    /// <summary>
    /// Creates an enabled-by-default Info descriptor in the AspNetCore category — a modernization nudge where the
    /// legacy form still works correctly, so a build-breaking Warning would be too strong.
    /// </summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateInfo(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "AspNetCore",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
