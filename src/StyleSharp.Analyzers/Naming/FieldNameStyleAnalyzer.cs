// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the alternative field-name style rules that conflict with this repository's runtime
/// <c>_camelCase</c> convention and are therefore disabled by default: field names beginning
/// with an upper-case letter (SST1306), field names prefixed with <c>m_</c>/<c>s_</c>/<c>t_</c>
/// (SST1308), and field names containing an underscore (SST1310). Enable them in
/// <c>.editorconfig</c> only if you prefer that style over SST1309.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FieldNameStyleAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The field-name prefixes flagged by SST1308.</summary>
    private static readonly string[] HungarianPrefixes = ["m_", "s_", "t_"];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        NamingRules.FieldLowerCase,
        NamingRules.NoFieldPrefix,
        NamingRules.FieldNoUnderscore);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    /// <summary>Checks each declared field name against the alternative field-name style rules.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        foreach (var variable in field.Declaration.Variables)
        {
            CheckName(context, variable.Identifier);
        }
    }

    /// <summary>Reports each style violation for a single field identifier.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="identifier">The field identifier token.</param>
    private static void CheckName(SyntaxNodeAnalysisContext context, SyntaxToken identifier)
    {
        var name = identifier.ValueText;
        if (name.Length == 0)
        {
            return;
        }

        var prefix = MatchingPrefix(name);
        if (prefix is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(NamingRules.NoFieldPrefix, identifier.GetLocation(), name, prefix));
        }

        if (name.IndexOf('_') >= 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(NamingRules.FieldNoUnderscore, identifier.GetLocation(), name));
        }

        if (!char.IsUpper(name[0]))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(NamingRules.FieldLowerCase, identifier.GetLocation(), name));
    }

    /// <summary>Returns the disallowed prefix the name starts with, or <see langword="null"/>.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>The matching prefix, or <see langword="null"/>.</returns>
    private static string? MatchingPrefix(string name)
    {
        foreach (var prefix in HungarianPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return prefix;
            }
        }

        return null;
    }
}
