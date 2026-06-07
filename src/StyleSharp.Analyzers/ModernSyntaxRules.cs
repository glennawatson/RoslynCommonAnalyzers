// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Diagnostic descriptors for modern C# syntax rules (SST22xx).</summary>
internal static class ModernSyntaxRules
{
    /// <summary>SST2200 — a single-use backing field can use the C# 14 <c>field</c> keyword.</summary>
    public static readonly DiagnosticDescriptor PreferFieldKeyword = new(
        "SST2200",
        "Prefer the field keyword",
        "Replace this single-use backing field with the 'field' keyword",
        "ModernSyntax",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "A property with accessor logic uses the C# 14 'field' keyword instead of a private single-use backing field.",
        helpLinkUri: "https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/SST2200.md");
}
