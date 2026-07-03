// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the concurrency and async performance rules (PSH13xx).
/// These target cheaper synchronization and task patterns, and are gated on the
/// relevant runtime type existing so they light up only where the replacement
/// compiles.
/// </summary>
internal static class ConcurrencyRules
{
    /// <summary>PSH1300 — a dedicated <c>object</c> lock field should be a <c>System.Threading.Lock</c>.</summary>
    public static readonly DiagnosticDescriptor PreferLockType = Create(
        "PSH1300",
        "Use System.Threading.Lock for a dedicated lock object",
        "Change the type of '{0}' to System.Threading.Lock",
        "A private readonly object used only as a lock target is declared as System.Threading.Lock (.NET 9+), which the compiler locks through a typed scope rather than Monitor.");

    /// <summary>PSH1301 — a single task does not need a WhenAll or WaitAll wrapper.</summary>
    public static readonly DiagnosticDescriptor AwaitSingleTaskDirectly = Create(
        "PSH1301",
        "Do not wrap a single task in WhenAll or WaitAll",
        "Use the task directly instead of '{0}'",
        "Task.WhenAll and Task.WaitAll with one task allocate an array and combining machinery to coordinate nothing; awaiting or waiting the task directly skips both.");

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
