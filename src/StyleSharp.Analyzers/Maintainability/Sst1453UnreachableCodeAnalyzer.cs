// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports statements that cannot execute because an earlier statement leaves the block (SST1453).
/// Local function declarations are hoisted rather than executed in place, and a labeled statement
/// can be entered by a <c>goto</c>, so neither is reported; both mirror the compiler, which raises
/// no CS0162 for them. A label conservatively restores reachability even when no <c>goto</c> targets
/// it — CS0162 already covers that case.
/// </summary>
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

            // A label can be entered by a goto, so it and everything after it run again.
            if (statement.IsKind(SyntaxKind.LabeledStatement))
            {
                unreachable = false;
                continue;
            }

            // A local function is a hoisted declaration, not code at this position. It is never
            // unreachable, and it does not make the statements after it reachable either.
            if (statement.IsKind(SyntaxKind.LocalFunctionStatement))
            {
                continue;
            }

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
