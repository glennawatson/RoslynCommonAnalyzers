// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the API selection rules (PSH14xx). These point at
/// cheaper framework APIs that do the same work with less allocation or setup,
/// and are gated on the replacement API existing in the referenced framework.
/// </summary>
internal static class ApiSelectionRules
{
    /// <summary>PSH1400 — one-shot hashing should use the static <c>HashData</c> methods.</summary>
    public static readonly DiagnosticDescriptor PreferStaticHashData = Create(
        "PSH1400",
        "Use the static HashData method for one-shot hashing",
        "Replace this create-and-compute pattern with {0}.HashData",
        "One-shot hashing through a HashAlgorithm instance allocates and disposes it for nothing; the static HashData methods (.NET 5+) hash in one call and are suggested only where they exist.");

    /// <summary>PSH1401 — attribute types should be sealed for faster reflection lookups.</summary>
    public static readonly DiagnosticDescriptor SealAttributeTypes = Create(
        "PSH1401",
        "Attribute types should be sealed",
        "Seal '{0}' so attribute lookups skip the inheritance search",
        "Reflection-based attribute lookups are cheaper on sealed attribute types because the runtime never has to consider derived attributes.");

    /// <summary>PSH1402 — a compile-time-constant static readonly field should be const.</summary>
    public static readonly DiagnosticDescriptor PreferConstOverStaticReadonly = Create(
        "PSH1402",
        "Use const for compile-time constants",
        "Make '{0}' const; its value is known at compile time",
        "A private or internal static readonly field with a constant value costs a field load on every use; const folds the value into call sites. Public fields are skipped.");

    /// <summary>PSH1403 — fields should not restate their default value.</summary>
    public static readonly DiagnosticDescriptor RemoveRedundantDefaultInitialization = Create(
        "PSH1403",
        "Do not initialize fields to their default value",
        "Remove this initializer; '{0}' already starts at its default value",
        "The runtime zero-initializes fields before any constructor runs, so explicitly assigning the default repeats the work in every constructor for nothing.");

    /// <summary>PSH1404 — the executing assembly is known statically.</summary>
    public static readonly DiagnosticDescriptor PreferTypeofAssembly = Create(
        "PSH1404",
        "Get the assembly from typeof instead of a stack walk",
        "Use typeof({0}).Assembly instead of Assembly.GetExecutingAssembly()",
        "Assembly.GetExecutingAssembly walks the call stack at runtime to discover its caller; typeof(T).Assembly resolves the same assembly statically.");

    /// <summary>PSH1405 — the runtime exposes direct process and thread properties.</summary>
    public static readonly DiagnosticDescriptor UseEnvironmentProperties = Create(
        "PSH1405",
        "Use the direct Environment APIs",
        "Use '{0}' instead of this call chain",
        "Environment.ProcessId, ProcessPath, and CurrentManagedThreadId read runtime state directly; the Process and Thread routes do needless work. Suggested only where the API exists.");

    /// <summary>PSH1406 — Regex can answer bool and count questions without materializing matches.</summary>
    public static readonly DiagnosticDescriptor UseDirectRegexQueries = Create(
        "PSH1406",
        "Ask Regex for the answer directly",
        "Use '{0}' instead of materializing the match",
        "Regex.Match(input).Success and Regex.Matches(input).Count allocate match objects to answer a bool or an int; IsMatch and Count answer directly. Suggested only where the API exists.");

    /// <summary>PSH1407 — key membership is the dictionary's own question.</summary>
    public static readonly DiagnosticDescriptor UseContainsKeyOverKeysContains = Create(
        "PSH1407",
        "Query the dictionary, not its Keys view",
        "Use ContainsKey instead of Keys.Contains",
        "dictionary.Keys.Contains(key) may allocate the keys view and enumerate it; ContainsKey is a single hash probe.");

    /// <summary>Creates a Warning-severity ApiSelection descriptor whose help link points at the rule's docs page.</summary>
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
            "ApiSelection",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
