// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a generic declaration whose <c>where</c> constraint clauses are not in the order of the type
/// parameters they constrain (SST1221). Reordering the clauses to follow the angle-bracket list lets a
/// reader line each constraint up with its parameter. The check is purely syntactic and only runs when a
/// declaration has at least two constraint clauses.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1221ConstraintClauseOrderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The fewest constraint clauses an ordering issue needs.</summary>
    private const int MinimumClauses = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(OrderingRules.ConstraintClauseOrder);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.DelegateDeclaration,
            SyntaxKind.LocalFunctionStatement);
    }

    /// <summary>Returns the first constraint clause that sorts before an earlier one, or <see langword="null"/>.</summary>
    /// <param name="typeParameters">The declaration's type-parameter list.</param>
    /// <param name="clauses">The declaration's constraint clauses.</param>
    /// <returns>The first out-of-order clause, or <see langword="null"/> when the clauses are ordered.</returns>
    internal static TypeParameterConstraintClauseSyntax? FindFirstOutOfOrderClause(
        TypeParameterListSyntax typeParameters,
        SyntaxList<TypeParameterConstraintClauseSyntax> clauses)
    {
        var lastPosition = -1;
        for (var i = 0; i < clauses.Count; i++)
        {
            var position = GenericConstraintLayout.PositionOf(typeParameters, clauses[i].Name.Identifier.ValueText);
            if (position < 0)
            {
                return null;
            }

            if (position < lastPosition)
            {
                return clauses[i];
            }

            lastPosition = position;
        }

        return null;
    }

    /// <summary>Reports a declaration whose constraint clauses are out of type-parameter order.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (!GenericConstraintLayout.TryGet(context.Node, out var typeParameters, out var clauses)
            || typeParameters is null
            || clauses.Count < MinimumClauses)
        {
            return;
        }

        if (FindFirstOutOfOrderClause(typeParameters, clauses) is not { } offending)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(OrderingRules.ConstraintClauseOrder, offending.GetLocation()));
    }
}
