// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports statements that cannot execute because an earlier statement leaves the block (SST1453).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1453UnreachableCodeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.NoUnreachableCode);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeBlock, SyntaxKind.Block);
    }

    /// <summary>Reports statements after a direct terminating statement in the same block.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeBlock(SyntaxNodeAnalysisContext context)
    {
        var statements = ((BlockSyntax)context.Node).Statements;
        var unreachable = false;
        for (var i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];
            if (unreachable)
            {
                context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoUnreachableCode, statement.GetLocation()));
                continue;
            }

            unreachable = IsTerminatingStatement(statement);
        }
    }

    /// <summary>Returns whether a statement always transfers control out of its current block.</summary>
    /// <param name="statement">The statement.</param>
    /// <returns><see langword="true"/> for direct jumps that make following statements unreachable.</returns>
    private static bool IsTerminatingStatement(StatementSyntax statement)
        => statement.Kind() is SyntaxKind.ReturnStatement
            or SyntaxKind.ThrowStatement
            or SyntaxKind.BreakStatement
            or SyntaxKind.ContinueStatement
            or SyntaxKind.GotoStatement;
}
