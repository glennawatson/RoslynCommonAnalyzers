// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a field or local declaration that declares more than one variable in a single statement
/// (<c>int x, y;</c>) (SST1132). Splitting them keeps each variable's modifiers, type, and initializer
/// unambiguous and makes later edits cleaner.
/// </summary>
/// <remarks>
/// A <c>for</c> initializer is never reported: a single declaration statement is the only way to declare more
/// than one loop variable there, so its combined form is not a choice. Because the initializer's
/// <see cref="VariableDeclarationSyntax"/> is not a <see cref="LocalDeclarationStatementSyntax"/>, the rule
/// never sees it — the same is true of a <c>using</c> or <c>fixed</c> statement's declaration.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1132DoNotCombineFieldsAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.DoNotCombineFields);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration, SyntaxKind.LocalDeclarationStatement);
    }

    /// <summary>Reports each variable beyond the first in a combined declaration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var variables = GetVariables(context.Node);
        if (variables.Count < 2)
        {
            return;
        }

        for (var index = 1; index < variables.Count; index++)
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.DoNotCombineFields, variables[index].Identifier.GetLocation()));
        }
    }

    /// <summary>Gets the declared variables of a field or local declaration statement.</summary>
    /// <param name="node">The field or local declaration statement.</param>
    /// <returns>The declarators, or an empty list when the node declares none.</returns>
    private static SeparatedSyntaxList<VariableDeclaratorSyntax> GetVariables(SyntaxNode node) => node switch
    {
        FieldDeclarationSyntax field => field.Declaration.Variables,
        LocalDeclarationStatementSyntax local => local.Declaration.Variables,
        _ => default,
    };
}
