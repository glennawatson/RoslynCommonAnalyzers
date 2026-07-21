// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the framework-convention rules (SST27xx). Each targets a shape a specific
/// application framework expects — the apartment state of a UI entry point, the lifetime a request-scoped
/// value may escape into — and is gated on the relevant framework type existing in the referenced
/// assemblies so a project that does not use that framework pays nothing.
/// </summary>
internal static partial class FrameworksRules
{
    /// <summary>Creates an enabled-by-default Warning descriptor in the Frameworks category.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Frameworks", description);

    /// <summary>
    /// Creates a disabled-by-default Warning descriptor in the Frameworks category — a heuristic whose shape
    /// carries some false-positive risk, so it is opt-in through <c>.editorconfig</c> rather than on by default.
    /// </summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateDisabled(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Frameworks",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: false,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
