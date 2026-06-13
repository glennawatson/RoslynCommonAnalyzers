// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Grouped readability analyzer that flags expressions which can be written in a simpler, more direct
/// form. One tree walk reports every id in the family so the rules share registration overhead.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST1172 — a comparison wrapped in a logical-not (<c>!(a == b)</c>) should use the opposite operator.</description></item>
/// <item><description>SST1173 — an anonymous-type member restates a name that would be inferred (<c>new { X = obj.X }</c>).</description></item>
/// <item><description>SST1175 — a cast targets the type the operand already has (<c>(int)anInt</c>).</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExpressionSimplificationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ReadabilityRules.NoInvertedBooleanCheck,
        ReadabilityRules.NoRedundantAnonymousTypeMemberName,
        ReadabilityRules.NoRedundantCast);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeInvertedBooleanCheck, SyntaxKind.LogicalNotExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAnonymousTypeMember, SyntaxKind.AnonymousObjectMemberDeclarator);
        context.RegisterSyntaxNodeAction(AnalyzeRedundantCast, SyntaxKind.CastExpression);
    }

    /// <summary>Returns the name an anonymous-type member would infer from its expression, or <see langword="null"/>.</summary>
    /// <param name="expression">The member initializer expression.</param>
    /// <returns>The inferred member name, or <see langword="null"/> when none can be inferred.</returns>
    internal static string? InferredName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
        _ => null
    };

    /// <summary>Returns whether a comparison kind is relational (<c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>).</summary>
    /// <param name="kind">The binary expression kind.</param>
    /// <returns><see langword="true"/> for a relational comparison.</returns>
    internal static bool IsRelational(SyntaxKind kind) => kind is SyntaxKind.LessThanExpression
        or SyntaxKind.LessThanOrEqualExpression
        or SyntaxKind.GreaterThanExpression
        or SyntaxKind.GreaterThanOrEqualExpression;

    /// <summary>Maps a comparison kind to the opposite expression kind, operator token, and operator text.</summary>
    /// <param name="kind">The comparison expression kind.</param>
    /// <param name="expressionKind">The opposite expression kind.</param>
    /// <param name="tokenKind">The opposite operator token kind.</param>
    /// <param name="text">The opposite operator text (for the diagnostic message).</param>
    /// <returns><see langword="true"/> when <paramref name="kind"/> is an invertible comparison.</returns>
    [SuppressMessage("Critical Code Smell", "S1541:Methods and properties should not be too complex", Justification = "A flat comparison-kind switch is a zero-allocation jump table.")]
    internal static bool TryGetOpposite(SyntaxKind kind, out SyntaxKind expressionKind, out SyntaxKind tokenKind, out string text)
    {
        (expressionKind, tokenKind, text) = kind switch
        {
            SyntaxKind.EqualsExpression => (SyntaxKind.NotEqualsExpression, SyntaxKind.ExclamationEqualsToken, "!="),
            SyntaxKind.NotEqualsExpression => (SyntaxKind.EqualsExpression, SyntaxKind.EqualsEqualsToken, "=="),
            SyntaxKind.LessThanExpression => (SyntaxKind.GreaterThanOrEqualExpression, SyntaxKind.GreaterThanEqualsToken, ">="),
            SyntaxKind.LessThanOrEqualExpression => (SyntaxKind.GreaterThanExpression, SyntaxKind.GreaterThanToken, ">"),
            SyntaxKind.GreaterThanExpression => (SyntaxKind.LessThanOrEqualExpression, SyntaxKind.LessThanEqualsToken, "<="),
            SyntaxKind.GreaterThanOrEqualExpression => (SyntaxKind.LessThanExpression, SyntaxKind.LessThanToken, "<"),
            _ => (SyntaxKind.None, SyntaxKind.None, string.Empty)
        };
        return tokenKind != SyntaxKind.None;
    }

    /// <summary>Unwraps any enclosing parentheses to reach the inner expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression.</returns>
    internal static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>Reports SST1172 when a <c>!</c> wraps an invertible comparison.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvertedBooleanCheck(SyntaxNodeAnalysisContext context)
    {
        var not = (PrefixUnaryExpressionSyntax)context.Node;
        if (Unwrap(not.Operand) is not BinaryExpressionSyntax binary
            || !TryGetOpposite(binary.Kind(), out _, out _, out var text))
        {
            return;
        }

        // Equality inversion is always safe. Relational inversion is only safe when neither operand
        // can be null or NaN, because '!(a < b)' folds those into 'true' but 'a >= b' into 'false'.
        if (IsRelational(binary.Kind())
            && (IsUnsafeRelationalOperand(binary.Left, context.SemanticModel, context.CancellationToken)
                || IsUnsafeRelationalOperand(binary.Right, context.SemanticModel, context.CancellationToken)))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoInvertedBooleanCheck, not.GetLocation(), text));
    }

    /// <summary>Reports SST1173 when an anonymous-type member explicitly restates its inferred name.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeAnonymousTypeMember(SyntaxNodeAnalysisContext context)
    {
        var declarator = (AnonymousObjectMemberDeclaratorSyntax)context.Node;
        if (declarator.NameEquals is not { } nameEquals
            || InferredName(declarator.Expression) is not { } inferred
            || !string.Equals(inferred, nameEquals.Name.Identifier.ValueText, StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantAnonymousTypeMemberName, nameEquals.Name.GetLocation(), inferred));
    }

    /// <summary>Reports SST1175 when a cast targets the operand's own type (including nullability).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeRedundantCast(SyntaxNodeAnalysisContext context)
    {
        var cast = (CastExpressionSyntax)context.Node;

        // 'default'/'default(T)' have no independent type, so a cast on them is never redundant noise.
        var operand = Unwrap(cast.Expression);
        if (operand.IsKind(SyntaxKind.DefaultLiteralExpression) || operand.IsKind(SyntaxKind.DefaultExpression))
        {
            return;
        }

        var operandType = context.SemanticModel.GetTypeInfo(cast.Expression, context.CancellationToken).Type;
        if (operandType is null)
        {
            return;
        }

        var targetType = context.SemanticModel.GetTypeInfo(cast.Type, context.CancellationToken).Type;

        // Compare with nullability so a cast that changes the null-state ('(string)maybeNull') is kept.
        if (targetType is null || !SymbolEqualityComparer.IncludeNullability.Equals(operandType, targetType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantCast, cast.Type.GetLocation(), targetType.ToDisplayString()));
    }

    /// <summary>Returns whether a relational operand is nullable, floating-point, or conditional-access.</summary>
    /// <param name="operand">The operand to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when inverting the comparison would not preserve the result.</returns>
    private static bool IsUnsafeRelationalOperand(ExpressionSyntax operand, SemanticModel model, CancellationToken cancellationToken)
    {
        if (operand.IsKind(SyntaxKind.ConditionalAccessExpression))
        {
            return true;
        }

        var type = model.GetTypeInfo(operand, cancellationToken).Type;
        return type is not null && (IsFloatingPoint(type) || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);
    }

    /// <summary>Returns whether a type is a floating-point type that has a <c>NaN</c> value.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for <see cref="float"/>, <see cref="double"/>, <c>Half</c>, or <c>NFloat</c>.</returns>
    private static bool IsFloatingPoint(ITypeSymbol type)
        => type.SpecialType is SpecialType.System_Single or SpecialType.System_Double
            || type.Name is "Half" or "NFloat";
}
