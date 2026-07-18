// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a bare local declaration whose first value is supplied by the immediately following
/// statement (SST2250): <c>int x;</c> followed by <c>x = 5;</c> reads more directly as
/// <c>int x = 5;</c>.
/// </summary>
/// <remarks>
/// The check is pure syntax and never binds a symbol. It fires only when the declaration is a single
/// uninitialized declarator inside a block and the very next statement is a straight-line simple
/// assignment to that same local. Requiring the assignment to be the immediately following statement
/// guarantees nothing reads the local in between and that joining moves neither evaluation order nor
/// definite assignment. A multi-declarator declaration, an already-initialized declaration, a
/// conditional or nested first write (the next statement is an <c>if</c>, loop, or a <c>ref</c>/<c>out</c>
/// use rather than a plain assignment), and a declaration that is the last statement in its block are
/// all left alone. Inside the block the local's name cannot be shadowed, so matching the assignment
/// target by name is exact without the semantic model.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2250JoinDeclarationAndAssignmentAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.JoinDeclarationAndAssignment);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
    }

    /// <summary>Returns whether a bare local declaration is joined by the next statement's first assignment.</summary>
    /// <param name="local">The local declaration to inspect.</param>
    /// <param name="variable">The single uninitialized declarator.</param>
    /// <param name="assignment">The following straight-line assignment statement.</param>
    /// <returns><see langword="true"/> when the declaration and its first assignment can be joined.</returns>
    internal static bool TryGetJoinCandidate(
        LocalDeclarationStatementSyntax local,
        out VariableDeclaratorSyntax variable,
        out ExpressionStatementSyntax assignment)
    {
        variable = null!;
        assignment = null!;
        if (local.Declaration.Variables.Count != 1
            || local.Declaration.Variables[0] is not { Initializer: null } declarator
            || local.Parent is not BlockSyntax block)
        {
            return false;
        }

        var statements = block.Statements;
        var index = statements.IndexOf(local);
        if (index + 1 >= statements.Count
            || statements[index + 1] is not ExpressionStatementSyntax next
            || next.Expression is not AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression, Left: IdentifierNameSyntax left }
            || left.Identifier.ValueText != declarator.Identifier.ValueText)
        {
            return false;
        }

        variable = declarator;
        assignment = next;
        return true;
    }

    /// <summary>Reports a joinable bare local declaration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context)
    {
        var local = (LocalDeclarationStatementSyntax)context.Node;
        if (!TryGetJoinCandidate(local, out var variable, out _))
        {
            return;
        }

        context.ReportDiagnostic(
            DiagnosticHelper.Create(
                ModernSyntaxRules.JoinDeclarationAndAssignment,
                variable.Identifier.GetLocation(),
                variable.Identifier.ValueText));
    }
}
