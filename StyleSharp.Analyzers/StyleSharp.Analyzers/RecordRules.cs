// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the record rules (SST18xx). These have no StyleCop
/// counterpart — they cover conventions specific to C# <c>record</c> and
/// <c>record struct</c> declarations (sealing, positional-parameter casing,
/// init-only properties, and readonly value records).
/// </summary>
internal static class RecordRules
{
    /// <summary>SST1800 — a record class is neither sealed nor abstract (opt-in).</summary>
    public static readonly DiagnosticDescriptor SealRecordClass = CreateOptIn(
        "SST1800",
        "Record classes should be sealed",
        "Seal record '{0}' or make it abstract",
        "A record class that is not part of an inheritance hierarchy is sealed so its compiler-generated equality is final. Off by default — record inheritance is a deliberate design choice.");

    /// <summary>SST1801 — a positional record parameter does not match the configured casing (default PascalCase).</summary>
    public static readonly DiagnosticDescriptor PositionalParameterNaming = Create(
        "SST1801",
        "Positional record parameters should match the configured casing",
        "Positional record parameter '{0}' should match the configured casing convention",
        "A positional record parameter becomes a public property, so it follows the configured casing (default PascalCase); set 'stylesharp.record_parameter_naming' in .editorconfig to override.");

    /// <summary>SST1802 — a record declares a settable (rather than init-only) instance property.</summary>
    public static readonly DiagnosticDescriptor InitOnlyProperty = Create(
        "SST1802",
        "Record properties should be init-only",
        "Replace the 'set' accessor of '{0}' with 'init'",
        "An instance property on a record uses 'init' rather than 'set' so the value semantics records provide are not undermined by mutation after construction.");

    /// <summary>SST1803 — a record struct is not declared readonly.</summary>
    public static readonly DiagnosticDescriptor ReadonlyRecordStruct = Create(
        "SST1803",
        "Record structs should be readonly",
        "Make record struct '{0}' readonly",
        "A record struct is declared 'readonly record struct' so the value type cannot mutate in place, matching how records are intended to be used.");

    /// <summary>Creates a Warning-severity Records descriptor whose help link points at the rule's docs page.</summary>
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
            "Records",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");

    /// <summary>Creates a Records descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Records",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: false,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
