// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a multidimensional array where it is declared or created (PSH1020) — <c>T[,]</c>. The CLR
/// gives a single-dimensional array a fast access path with elided bounds checks that a
/// multidimensional array never gets: every <c>a[i, j]</c> goes through a slower helper, and the JIT
/// cannot hoist the check out of a loop. A jagged <c>T[][]</c> is an array of single-dimensional
/// arrays, so every row is back on the fast path.
/// </summary>
/// <remarks>
/// <para>
/// <b>No code fix, on purpose.</b> Changing the shape is not a local edit: every <c>a[i, j]</c>
/// becomes <c>a[i][j]</c>, every allocation becomes a loop that fills the rows, <c>GetLength(1)</c>
/// stops meaning what it meant, and a rectangular array's guarantee that all rows are the same length
/// is lost. Those are decisions about the code, not mechanical rewrites, so the rule reports and
/// leaves them to the author.
/// </para>
/// <para>
/// <b>Only declarations and creations.</b> A <c>typeof(int[,])</c>, a cast, or a type argument merely
/// names a multidimensional array that already exists — often one handed over by an API the author
/// does not own — and there is nothing to change at those sites, so they are not reported. Reporting
/// is confined to the places the shape is actually chosen: a field, a local, a parameter, a property,
/// a method's return type, and an array creation.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1020PreferJaggedArraysAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The lowest rank that makes an array multidimensional.</summary>
    private const int MultidimensionalRank = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.PreferJaggedArrays);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeArrayType, SyntaxKind.ArrayType);
        context.RegisterSyntaxNodeAction(AnalyzeImplicitArrayCreation, SyntaxKind.ImplicitArrayCreationExpression);
    }

    /// <summary>Reports PSH1020 for a written-out multidimensional array type in a declaring position.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeArrayType(SyntaxNodeAnalysisContext context)
    {
        var arrayType = (ArrayTypeSyntax)context.Node;
        if (!IsMultidimensional(arrayType) || !IsDeclaringPosition(arrayType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.PreferJaggedArrays,
            arrayType.SyntaxTree,
            arrayType.Span,
            arrayType.ToString()));
    }

    /// <summary>Reports PSH1020 for an implicitly typed multidimensional array creation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <remarks>
    /// <c>new[,] { { 1, 2 }, { 3, 4 } }</c> writes no type at all, so it carries no
    /// <see cref="ArrayTypeSyntax"/> to match — the commas in the brackets are the only evidence that
    /// the array is rectangular.
    /// </remarks>
    private static void AnalyzeImplicitArrayCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ImplicitArrayCreationExpressionSyntax)context.Node;
        if (creation.Commas.Count == 0)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.PreferJaggedArrays,
            creation.SyntaxTree,
            creation.Span,
            "new[" + new string(',', creation.Commas.Count) + "]"));
    }

    /// <summary>Returns whether an array type has a rank specifier of two dimensions or more.</summary>
    /// <param name="arrayType">The array type to inspect.</param>
    /// <returns><see langword="true"/> when the type is rectangular somewhere in its shape.</returns>
    /// <remarks>
    /// A jagged <c>int[][]</c> carries two rank specifiers of one dimension each and is exactly what
    /// the rule is asking for, so it never matches; <c>int[,][]</c> does, on its rectangular outer
    /// specifier.
    /// </remarks>
    private static bool IsMultidimensional(ArrayTypeSyntax arrayType)
    {
        var specifiers = arrayType.RankSpecifiers;
        for (var i = 0; i < specifiers.Count; i++)
        {
            if (specifiers[i].Rank >= MultidimensionalRank)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an array type is written where the array's shape is being chosen.</summary>
    /// <param name="arrayType">The array type to inspect.</param>
    /// <returns><see langword="true"/> for a declaration or a creation the author owns.</returns>
    private static bool IsDeclaringPosition(ArrayTypeSyntax arrayType)
        => arrayType.Parent switch
        {
            VariableDeclarationSyntax => true,
            ParameterSyntax => true,
            PropertyDeclarationSyntax => true,
            MethodDeclarationSyntax => true,
            DelegateDeclarationSyntax => true,
            ArrayCreationExpressionSyntax creation => !IsRedundantWithDeclaredType(creation),
            _ => false,
        };

    /// <summary>Returns whether a creation's type was already reported on the variable it initializes.</summary>
    /// <param name="creation">The array creation.</param>
    /// <returns><see langword="true"/> when reporting the creation would duplicate the declaration.</returns>
    /// <remarks>
    /// <c>int[,] grid = new int[3, 4];</c> chooses the shape once and writes it twice. The declaration
    /// is the site the author edits, so the creation stays quiet — but only when the declaration really
    /// did spell the type out. Under <c>var</c>, the creation is the only site there is.
    /// </remarks>
    private static bool IsRedundantWithDeclaredType(ArrayCreationExpressionSyntax creation)
        => creation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Type: ArrayTypeSyntax declared } } }
            && IsMultidimensional(declared);
}
