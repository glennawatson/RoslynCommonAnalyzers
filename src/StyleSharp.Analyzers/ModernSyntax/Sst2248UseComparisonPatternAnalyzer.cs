// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports two comparisons of the same side-effect-free value against constants, joined by
/// <c>&amp;&amp;</c> or <c>||</c>, that fold into a single <c>is</c>-pattern (SST2248):
/// <c>x &gt;= 0 &amp;&amp; x &lt;= 9</c> becomes <c>x is &gt;= 0 and &lt;= 9</c>,
/// <c>t == A || t == B</c> becomes <c>t is A or B</c>, and <c>n &lt; 0 || n &gt; 100</c>
/// becomes <c>n is &lt; 0 or &gt; 100</c>.
/// </summary>
/// <remarks>
/// <para>
/// The rule is gated on C# 9, where relational patterns and the <c>and</c>/<c>or</c> pattern
/// combinators arrive. The subject must be a bare local, field, or parameter read of an integral,
/// <c>char</c>, or enum type, so folding two reads into one cannot change what the program observes;
/// a property, indexer, method call, member access, or any differing subject is left alone.
/// </para>
/// <para>
/// Only combinations whose merged pattern reproduces the original result exactly are reported. For
/// <c>&amp;&amp;</c> that is a non-empty bounded range (a lower and an upper bound) or a pair of
/// inequalities; for <c>||</c> it is a pair of equalities or the non-empty region outside a range.
/// Combinations that would compile to a pattern the compiler proves always or never matches — the
/// dead <c>x == 1 &amp;&amp; x == 2</c>, the tautological <c>x &gt;= 0 || x &lt;= 9</c> — are skipped,
/// so the fix never introduces an error or a warning. The clean path is a syntax-kind test that
/// rejects every ordinary <c>&amp;&amp;</c>/<c>||</c> before the semantic model is consulted.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2248UseComparisonPatternAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 9 language-version value.</summary>
    private const int CSharp9 = 900;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseComparisonPattern);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LogicalAndExpression, SyntaxKind.LogicalOrExpression);
    }

    /// <summary>Validates a <c>&amp;&amp;</c>/<c>||</c> node and extracts the parts of its merged pattern.</summary>
    /// <param name="binary">The logical-and or logical-or expression.</param>
    /// <param name="model">The semantic model for the node's tree.</param>
    /// <param name="merge">The extracted merge, when the node is a reportable combination.</param>
    /// <returns><see langword="true"/> when the node folds into a single faithful is-pattern.</returns>
    internal static bool TryGetComparisonMerge(BinaryExpressionSyntax binary, SemanticModel model, out ComparisonPatternMerge merge)
    {
        merge = default;
        if (!TryClassifyComparison(binary.Left, model, out var subjectLeft, out var operatorLeft, out var constantLeft)
            || !TryClassifyComparison(binary.Right, model, out var subjectRight, out var operatorRight, out var constantRight))
        {
            return false;
        }

        if (!TryGetSharedSubjectType(model, subjectLeft, subjectRight, out var type)
            || !IsPatternConstant(model, constantLeft, type)
            || !IsPatternConstant(model, constantRight, type))
        {
            return false;
        }

        var valueLeft = ConstantValue(model, constantLeft);
        var valueRight = ConstantValue(model, constantRight);
        var isConjunction = binary.IsKind(SyntaxKind.LogicalAndExpression);
        var reportable = isConjunction
            ? IsFaithfulConjunction(operatorLeft, valueLeft, operatorRight, valueRight)
            : IsFaithfulDisjunction(operatorLeft, valueLeft, operatorRight, valueRight);
        if (!reportable)
        {
            return false;
        }

        merge = new ComparisonPatternMerge(subjectLeft, operatorLeft, constantLeft, operatorRight, constantRight, isConjunction);
        return true;
    }

    /// <summary>Reports a foldable comparison combination.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (binary.SyntaxTree.Options is not CSharpParseOptions options
            || (int)options.LanguageVersion < CSharp9
            || !TryGetComparisonMerge(binary, context.SemanticModel, out _))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseComparisonPattern, binary.GetLocation()));
    }

    /// <summary>Splits one comparison into its subject, its subject-on-left operator, and its constant.</summary>
    /// <param name="operand">The candidate comparison operand.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="subject">The bare identifier read being compared.</param>
    /// <param name="comparison">The comparison kind rewritten so the subject sits on the left.</param>
    /// <param name="constant">The constant the subject is compared against.</param>
    /// <returns><see langword="true"/> when the operand compares a bare identifier with a constant.</returns>
    private static bool TryClassifyComparison(
        ExpressionSyntax operand,
        SemanticModel model,
        out ExpressionSyntax subject,
        out SyntaxKind comparison,
        out ExpressionSyntax constant)
    {
        subject = null!;
        constant = null!;
        comparison = SyntaxKind.None;
        if (operand is not BinaryExpressionSyntax candidate || !IsComparisonKind(candidate.Kind()))
        {
            return false;
        }

        if (IsSubjectAndConstant(model, candidate.Left, candidate.Right))
        {
            subject = candidate.Left;
            constant = candidate.Right;
            comparison = candidate.Kind();
            return true;
        }

        if (!IsSubjectAndConstant(model, candidate.Right, candidate.Left))
        {
            return false;
        }

        subject = candidate.Right;
        constant = candidate.Left;
        comparison = Flip(candidate.Kind());
        return true;
    }

    /// <summary>Returns whether one operand is a bare non-constant identifier and the other a constant.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="subjectCandidate">The operand that would be the subject.</param>
    /// <param name="constantCandidate">The operand that would be the constant.</param>
    /// <returns><see langword="true"/> when the pair reads as subject-and-constant.</returns>
    private static bool IsSubjectAndConstant(SemanticModel model, ExpressionSyntax subjectCandidate, ExpressionSyntax constantCandidate)
        => subjectCandidate is IdentifierNameSyntax
            && !model.GetConstantValue(subjectCandidate).HasValue
            && model.GetConstantValue(constantCandidate).HasValue;

    /// <summary>Resolves the two subjects to one shared local, parameter, or field of a supported type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="subjectLeft">The left comparison's subject.</param>
    /// <param name="subjectRight">The right comparison's subject.</param>
    /// <param name="type">The shared subject's type.</param>
    /// <returns><see langword="true"/> when both name the same supported variable.</returns>
    private static bool TryGetSharedSubjectType(SemanticModel model, ExpressionSyntax subjectLeft, ExpressionSyntax subjectRight, out ITypeSymbol type)
    {
        var symbolLeft = model.GetSymbolInfo(subjectLeft).Symbol;
        if (!TryGetVariableType(symbolLeft, out type))
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(symbolLeft, model.GetSymbolInfo(subjectRight).Symbol)
            && IsSupportedType(type);
    }

    /// <summary>Returns whether a syntax kind is one of the six relational or equality comparisons.</summary>
    /// <param name="kind">The operand's syntax kind.</param>
    /// <returns><see langword="true"/> for <c>&lt; &lt;= &gt; &gt;= == !=</c>.</returns>
    private static bool IsComparisonKind(SyntaxKind kind)
        => kind is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression
            or SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression;

    /// <summary>Mirrors a comparison so its constant can move from the left of the operator to the right.</summary>
    /// <param name="kind">The comparison kind with the constant on the left.</param>
    /// <returns>The equivalent comparison kind with the subject on the left.</returns>
    private static SyntaxKind Flip(SyntaxKind kind)
        => kind switch
        {
            SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
            SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
            _ => kind,
        };

    /// <summary>Reads the declared type of a local, parameter, or field symbol.</summary>
    /// <param name="symbol">The subject symbol, or <see langword="null"/> when the name did not bind.</param>
    /// <param name="type">The symbol's type.</param>
    /// <returns><see langword="true"/> when the symbol is a plain variable read.</returns>
    private static bool TryGetVariableType(ISymbol? symbol, out ITypeSymbol type)
    {
        type = symbol switch
        {
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            IFieldSymbol field => field.Type,
            _ => null!,
        };
        return type is not null;
    }

    /// <summary>Returns whether a type supports both relational and equality patterns without surprise.</summary>
    /// <param name="type">The subject type.</param>
    /// <returns><see langword="true"/> for the integral primitives, <c>char</c>, and enums.</returns>
    private static bool IsSupportedType(ITypeSymbol type)
        => type.TypeKind == TypeKind.Enum
            || type.SpecialType is SpecialType.System_SByte or SpecialType.System_Byte
                or SpecialType.System_Int16 or SpecialType.System_UInt16
                or SpecialType.System_Int32 or SpecialType.System_UInt32
                or SpecialType.System_Int64 or SpecialType.System_UInt64
                or SpecialType.System_Char;

    /// <summary>Returns whether a constant can be used as a pattern operand against the subject type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="constant">The constant expression.</param>
    /// <param name="type">The subject type the pattern matches on.</param>
    /// <returns><see langword="true"/> when the constant converts into the pattern's input type.</returns>
    private static bool IsPatternConstant(SemanticModel model, ExpressionSyntax constant, ITypeSymbol type)
    {
        var conversion = model.ClassifyConversion(constant, type);
        return conversion.IsIdentity || conversion.IsImplicit;
    }

    /// <summary>Reads a supported constant as a decimal so bounds across the integral types compare uniformly.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="constant">The constant expression, already proven convertible to the subject type.</param>
    /// <returns>The constant's integer value as a decimal.</returns>
    private static decimal ConstantValue(SemanticModel model, ExpressionSyntax constant)
    {
        var value = model.GetConstantValue(constant).Value!;
        return value is char character ? character : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    /// <summary>Returns whether a <c>&amp;&amp;</c> of two comparisons folds into a pattern with no dead or trivial result.</summary>
    /// <param name="operatorLeft">The left comparison, subject on the left.</param>
    /// <param name="valueLeft">The left constant.</param>
    /// <param name="operatorRight">The right comparison, subject on the left.</param>
    /// <param name="valueRight">The right constant.</param>
    /// <returns><see langword="true"/> for a non-empty bounded range or two inequalities.</returns>
    private static bool IsFaithfulConjunction(SyntaxKind operatorLeft, decimal valueLeft, SyntaxKind operatorRight, decimal valueRight)
    {
        if (IsLowerBound(operatorLeft) && IsUpperBound(operatorRight))
        {
            return IsNonEmptyRange(operatorLeft, valueLeft, operatorRight, valueRight);
        }

        if (IsUpperBound(operatorLeft) && IsLowerBound(operatorRight))
        {
            return IsNonEmptyRange(operatorRight, valueRight, operatorLeft, valueLeft);
        }

        return IsInequality(operatorLeft) && IsInequality(operatorRight);
    }

    /// <summary>Returns whether a <c>||</c> of two comparisons folds into a pattern with no dead or trivial result.</summary>
    /// <param name="operatorLeft">The left comparison, subject on the left.</param>
    /// <param name="valueLeft">The left constant.</param>
    /// <param name="operatorRight">The right comparison, subject on the left.</param>
    /// <param name="valueRight">The right constant.</param>
    /// <returns><see langword="true"/> for two equalities or a non-empty region outside a range.</returns>
    private static bool IsFaithfulDisjunction(SyntaxKind operatorLeft, decimal valueLeft, SyntaxKind operatorRight, decimal valueRight)
    {
        if (IsEquality(operatorLeft) && IsEquality(operatorRight))
        {
            return true;
        }

        if (IsUpperBound(operatorLeft) && IsLowerBound(operatorRight))
        {
            return IsNonEmptyGap(operatorLeft, valueLeft, operatorRight, valueRight);
        }

        return IsLowerBound(operatorLeft) && IsUpperBound(operatorRight)
            && IsNonEmptyGap(operatorRight, valueRight, operatorLeft, valueLeft);
    }

    /// <summary>Returns whether the intersection of a lower and an upper bound holds for at least one integer.</summary>
    /// <param name="lowerBound">The lower-bound comparison (<c>&gt;</c> or <c>&gt;=</c>).</param>
    /// <param name="lowerValue">The lower-bound constant.</param>
    /// <param name="upperBound">The upper-bound comparison (<c>&lt;</c> or <c>&lt;=</c>).</param>
    /// <param name="upperValue">The upper-bound constant.</param>
    /// <returns><see langword="true"/> when the inclusive integer range is non-empty.</returns>
    private static bool IsNonEmptyRange(SyntaxKind lowerBound, decimal lowerValue, SyntaxKind upperBound, decimal upperValue)
    {
        var minimum = lowerBound == SyntaxKind.GreaterThanOrEqualExpression ? lowerValue : lowerValue + 1;
        var maximum = upperBound == SyntaxKind.LessThanOrEqualExpression ? upperValue : upperValue - 1;
        return minimum <= maximum;
    }

    /// <summary>Returns whether the region left uncovered by a low ray and a high ray holds for at least one integer.</summary>
    /// <param name="lowRay">The upper-bound comparison covering small values (<c>&lt;</c> or <c>&lt;=</c>).</param>
    /// <param name="lowValue">The low ray's constant.</param>
    /// <param name="highRay">The lower-bound comparison covering large values (<c>&gt;</c> or <c>&gt;=</c>).</param>
    /// <param name="highValue">The high ray's constant.</param>
    /// <returns><see langword="true"/> when the uncovered gap is non-empty, so the disjunction is not always true.</returns>
    private static bool IsNonEmptyGap(SyntaxKind lowRay, decimal lowValue, SyntaxKind highRay, decimal highValue)
    {
        var gapMinimum = lowRay == SyntaxKind.LessThanExpression ? lowValue : lowValue + 1;
        var gapMaximum = highRay == SyntaxKind.GreaterThanExpression ? highValue : highValue - 1;
        return gapMinimum <= gapMaximum;
    }

    /// <summary>Returns whether a comparison bounds the subject from below.</summary>
    /// <param name="kind">The subject-on-left comparison kind.</param>
    /// <returns><see langword="true"/> for <c>&gt;</c> and <c>&gt;=</c>.</returns>
    private static bool IsLowerBound(SyntaxKind kind)
        => kind is SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression;

    /// <summary>Returns whether a comparison bounds the subject from above.</summary>
    /// <param name="kind">The subject-on-left comparison kind.</param>
    /// <returns><see langword="true"/> for <c>&lt;</c> and <c>&lt;=</c>.</returns>
    private static bool IsUpperBound(SyntaxKind kind)
        => kind is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression;

    /// <summary>Returns whether a comparison is an equality test.</summary>
    /// <param name="kind">The subject-on-left comparison kind.</param>
    /// <returns><see langword="true"/> for <c>==</c>.</returns>
    private static bool IsEquality(SyntaxKind kind)
        => kind == SyntaxKind.EqualsExpression;

    /// <summary>Returns whether a comparison is an inequality test.</summary>
    /// <param name="kind">The subject-on-left comparison kind.</param>
    /// <returns><see langword="true"/> for <c>!=</c>.</returns>
    private static bool IsInequality(SyntaxKind kind)
        => kind == SyntaxKind.NotEqualsExpression;
}
