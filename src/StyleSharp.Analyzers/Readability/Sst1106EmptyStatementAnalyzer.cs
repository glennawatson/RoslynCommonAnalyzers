// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an empty statement — a stray <c>;</c> that carries no meaning (SST1106). Labeled
/// empty statements are left alone because the label is the meaningful part.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1106EmptyStatementAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.EmptyStatement);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EmptyStatement);
    }

    /// <summary>Reports an empty statement.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        // A labeled empty statement ('label: ;') keeps the semicolon as the label's target.
        if (context.Node.Parent is LabeledStatementSyntax)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.EmptyStatement, context.Node.GetLocation()));
    }
}
