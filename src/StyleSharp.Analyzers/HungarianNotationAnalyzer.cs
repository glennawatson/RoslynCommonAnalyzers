// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports field, parameter, and local names that appear to use Hungarian notation — a one-
/// or two-letter lower-case type prefix immediately followed by an upper-case letter
/// (SST1305). A small allow-list of common English words (such as <c>is</c>, <c>to</c>,
/// <c>id</c>) is excluded. Disabled by default; this is a heuristic and opt-in rule.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HungarianNotationAnalyzer : DiagnosticAnalyzer
{
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
        if (!IsHungarian(identifier.ValueText))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(NamingRules.NoHungarian, identifier.GetLocation(), identifier.ValueText));
    }

    /// <summary>Returns whether a name looks like Hungarian notation.</summary>
    /// <param name="name">The identifier name.</param>
    /// <returns><see langword="true"/> when the name has a short lower-case prefix before an upper-case letter.</returns>
    private static bool IsHungarian(string name)
    {
        var core = name.TrimStart('_');
        var prefix = 0;
        while (prefix < core.Length && char.IsLower(core[prefix]))
        {
            prefix++;
        }

        return prefix is not 0 and <= MaxPrefixLength
               && prefix < core.Length
               && char.IsUpper(core[prefix])
               && !AllowedPrefixes.Contains(core[..prefix]);
    }
}
