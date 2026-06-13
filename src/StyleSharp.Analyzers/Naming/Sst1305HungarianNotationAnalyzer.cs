// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports field, parameter, and local names that appear to use Hungarian notation — a one-
/// or two-letter lower-case type prefix immediately followed by an upper-case letter
/// (SST1305). A small allow-list of common English words (such as <c>is</c>, <c>to</c>,
/// <c>id</c>) is excluded, and more prefixes can be allowed via
/// <c>stylesharp.SST1305.allowed_hungarian_prefixes</c> (or the general
/// <c>stylesharp.allowed_hungarian_prefixes</c>) in <c>.editorconfig</c> as a comma-separated
/// list. Disabled by default; this is a heuristic and opt-in rule.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1305HungarianNotationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The rule-specific editorconfig key for additional allowed prefixes.</summary>
    private const string AllowedPrefixesSpecificKey = "stylesharp.SST1305.allowed_hungarian_prefixes";

    /// <summary>The general editorconfig key for additional allowed prefixes.</summary>
    private const string AllowedPrefixesGeneralKey = "stylesharp.allowed_hungarian_prefixes";

    /// <summary>The maximum length of a prefix considered a Hungarian type abbreviation.</summary>
    private const int MaxPrefixLength = 2;

    /// <summary>Short lower-case words that look like prefixes but are legitimate name starts.</summary>
    private static readonly HashSet<string> AllowedPrefixes = new(StringComparer.Ordinal)
    {
        "as", "at", "by", "db", "do", "go", "id", "if", "in", "io", "is", "it",
        "my", "no", "of", "on", "or", "so", "to", "ui", "up", "us", "ok"
    };

    /// <summary>The kinds whose identifiers are inspected for Hungarian notation.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.FieldDeclaration,
        SyntaxKind.Parameter,
        SyntaxKind.LocalDeclarationStatement);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(NamingRules.NoHungarian);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Inspects the identifiers declared by the node for Hungarian notation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        switch (context.Node)
        {
            case FieldDeclarationSyntax field:
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    Report(context, variable.Identifier);
                }

                break;
            }

            case LocalDeclarationStatementSyntax local:
            {
                foreach (var variable in local.Declaration.Variables)
                {
                    Report(context, variable.Identifier);
                }

                break;
            }

            case ParameterSyntax parameter:
            {
                Report(context, parameter.Identifier);
                break;
            }
        }
    }

    /// <summary>Reports the identifier when its name appears to use Hungarian notation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="identifier">The identifier token.</param>
    private static void Report(SyntaxNodeAnalysisContext context, SyntaxToken identifier)
    {
        if (!TryGetHungarianPrefix(identifier.ValueText, out var prefix) || IsAllowedPrefix(context, prefix))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(NamingRules.NoHungarian, identifier.GetLocation(), identifier.ValueText));
    }

    /// <summary>Returns the short lower-case prefix when a name looks like Hungarian notation.</summary>
    /// <param name="name">The identifier name.</param>
    /// <param name="prefix">The lower-case prefix when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the name has a short lower-case prefix before an upper-case letter.</returns>
    private static bool TryGetHungarianPrefix(string name, out string prefix)
    {
        prefix = string.Empty;
        var core = name.TrimStart('_');
        var length = 0;
        while (length < core.Length && char.IsLower(core[length]))
        {
            length++;
        }

        if (length is 0 or > MaxPrefixLength || length >= core.Length || !char.IsUpper(core[length]))
        {
            return false;
        }

        prefix = core[..length];
        return true;
    }

    /// <summary>Returns whether a candidate prefix is allowed by the built-in set or the configured list.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="prefix">The candidate Hungarian prefix.</param>
    /// <returns><see langword="true"/> when the prefix should not be flagged.</returns>
    private static bool IsAllowedPrefix(SyntaxNodeAnalysisContext context, string prefix)
    {
        if (AllowedPrefixes.Contains(prefix))
        {
            return true;
        }

        // The config is only read on the rare candidate path, after the cheap syntactic check passes.
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
        return ConfiguredListContains(options, AllowedPrefixesSpecificKey, prefix)
            || ConfiguredListContains(options, AllowedPrefixesGeneralKey, prefix);
    }

    /// <summary>Returns whether a comma- or whitespace-separated editorconfig list contains a prefix.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <param name="key">The editorconfig key to read.</param>
    /// <param name="prefix">The prefix to find.</param>
    /// <returns><see langword="true"/> when the list is present and contains the prefix.</returns>
    private static bool ConfiguredListContains(AnalyzerConfigOptions options, string key, string prefix)
        => options.TryGetValue(key, out var list) && list.Length != 0 && ListContainsToken(list, prefix);

    /// <summary>Returns whether a comma- or whitespace-separated list contains an exact (case-insensitive) token.</summary>
    /// <param name="list">The raw list value.</param>
    /// <param name="token">The token to find.</param>
    /// <returns><see langword="true"/> when the token appears in the list.</returns>
    private static bool ListContainsToken(string list, string token)
    {
        var start = 0;
        for (var i = 0; i <= list.Length; i++)
        {
            if (i != list.Length && !IsSeparator(list[i]))
            {
                continue;
            }

            if (TokenEquals(list, start, i - start, token))
            {
                return true;
            }

            start = i + 1;
        }

        return false;
    }

    /// <summary>Returns whether a character separates list entries.</summary>
    /// <param name="value">The character to test.</param>
    /// <returns><see langword="true"/> for a comma, semicolon, space, or tab.</returns>
    private static bool IsSeparator(char value) => value is ',' or ';' or ' ' or '\t';

    /// <summary>Returns whether a slice of a list equals a token, ignoring case.</summary>
    /// <param name="list">The raw list value.</param>
    /// <param name="start">The slice start index.</param>
    /// <param name="length">The slice length.</param>
    /// <param name="token">The token to compare against.</param>
    /// <returns><see langword="true"/> when the slice equals the token.</returns>
    private static bool TokenEquals(string list, int start, int length, string token)
        => length == token.Length && string.Compare(list, start, token, 0, length, StringComparison.OrdinalIgnoreCase) == 0;
}
