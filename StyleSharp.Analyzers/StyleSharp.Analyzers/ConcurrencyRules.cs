// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the concurrency rules (SST19xx). These cover locking
/// conventions, including the .NET 9 <c>System.Threading.Lock</c> type, and are
/// gated on the relevant runtime type existing so they light up only where the
/// replacement compiles.
/// </summary>
internal static class ConcurrencyRules
{
    /// <summary>SST1900 — a dedicated <c>object</c> lock field should be a <c>System.Threading.Lock</c>.</summary>
    public static readonly DiagnosticDescriptor PreferLockType = Create(
        "SST1900",
        "Use System.Threading.Lock for a dedicated lock object",
        "Change the type of '{0}' to System.Threading.Lock",
        "A private readonly object used only as a lock target is declared as System.Threading.Lock (.NET 9+), which the compiler locks through a typed scope rather than Monitor.");

    /// <summary>Creates a Warning-severity Concurrency descriptor whose help link points at the rule's docs page.</summary>
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
            "Concurrency",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
