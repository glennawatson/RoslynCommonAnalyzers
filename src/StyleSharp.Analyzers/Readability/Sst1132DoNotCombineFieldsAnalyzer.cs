// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a field declaration that declares more than one field in a single statement
/// (<c>int x, y;</c>) (SST1132). Splitting them keeps each field's modifiers, type, and initializer
/// unambiguous and makes later edits cleaner.
/// </summary>
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

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    /// <summary>Reports each field beyond the first in a combined declaration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var variables = ((FieldDeclarationSyntax)context.Node).Declaration.Variables;
        if (variables.Count < 2)
        {
            return;
        }

        for (var index = 1; index < variables.Count; index++)
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.DoNotCombineFields, variables[index].Identifier.GetLocation()));
        }
    }
}
