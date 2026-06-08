// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Requires local variables to use the .NET camelCase convention (SST1312).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1312LocalVariableNamingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(NamingRules.LocalCamelCase);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LocalDeclarationStatement, SyntaxKind.ForEachStatement);
    }

    /// <summary>Analyzes a local declaration or foreach loop variable for camelCase.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        switch (context.Node)
        {
            case LocalDeclarationStatementSyntax local when !ModifierListHelper.Contains(local.Modifiers, SyntaxKind.ConstKeyword):
            {
                foreach (var variable in local.Declaration.Variables)
                {
                    CheckCamelCase(context, variable.Identifier);
                }

                break;
            }

            case ForEachStatementSyntax forEach:
            {
                CheckCamelCase(context, forEach.Identifier);
                break;
            }
        }
    }

    /// <summary>Reports SST1312 when <paramref name="identifier"/> does not begin with a lower-case letter.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="identifier">The identifier token to check.</param>
    private static void CheckCamelCase(SyntaxNodeAnalysisContext context, SyntaxToken identifier)
    {
        var name = identifier.ValueText;
        if (NamingHelper.IsAllUnderscores(name) || NamingHelper.BeginsWithLowerCase(name))
        {
            return;
        }

        NamingDiagnostic.Report(context, NamingRules.LocalCamelCase, identifier, NamingHelper.SuggestCamelCase(name));
    }
}
