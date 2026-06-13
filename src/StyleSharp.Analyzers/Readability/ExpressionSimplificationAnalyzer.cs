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
/// <item><description>SST1182 — a conditional expression yields the boolean literals (<c>c ? true : false</c>).</description></item>
/// <item><description>SST1183 — an interpolated string has no interpolations.</description></item>
/// <item><description>SST1184 — a verbatim string needs no verbatim quoting.</description></item>
/// <item><description>SST1185 — an assignment recomputes its target (<c>x = x + y</c>) instead of using a compound operator.</description></item>
/// <item><description>SST1186 — a literal sits on the left of a comparison (<c>0 == n</c>).</description></item>
/// <item><description>SST1187 — an assignment is chained as the value of another assignment (<c>a = b = c</c>).</description></item>
/// <item><description>SST1188 — a <c>default(T)</c> is written where the bare <c>default</c> literal suffices.</description></item>
/// <item><description>SST1189 — an assignment copies a side-effect-free target onto itself (<c>x = x</c>).</description></item>
/// <item><description>SST1190 — a prefix-negation operator is applied twice (<c>!!x</c>, <c>~~x</c>).</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExpressionSimplificationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ReadabilityRules.NoInvertedBooleanCheck,
        ReadabilityRules.NoRedundantAnonymousTypeMemberName,
        ReadabilityRules.NoRedundantCast,
        ReadabilityRules.NoConditionalBooleanLiteral,
        ReadabilityRules.NoRedundantInterpolatedString,
        ReadabilityRules.NoRedundantVerbatimString,
        ReadabilityRules.UseCompoundAssignment,
        ReadabilityRules.LiteralOnRightOfComparison,
        ReadabilityRules.NoChainedAssignment,
        ReadabilityRules.UseDefaultLiteral,
        ReadabilityRules.NoSelfAssignment,
        ReadabilityRules.NoDoubledNegation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeInvertedBooleanCheck, SyntaxKind.LogicalNotExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAnonymousTypeMember, SyntaxKind.AnonymousObjectMemberDeclarator);
        context.RegisterSyntaxNodeAction(AnalyzeRedundantCast, SyntaxKind.CastExpression);
        context.RegisterSyntaxNodeAction(AnalyzeConditionalBooleanLiteral, SyntaxKind.ConditionalExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInterpolatedString, SyntaxKind.InterpolatedStringExpression);
        context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
        context.RegisterSyntaxNodeAction(AnalyzeSimpleAssignment, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeComparison, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeDefaultExpression, SyntaxKind.DefaultExpression);
        context.RegisterSyntaxNodeAction(AnalyzeDoubledNegation, SyntaxKind.LogicalNotExpression, SyntaxKind.BitwiseNotExpression);
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
    [SuppressMessage("Critical Code Smell", "the rule:Methods and properties should not be too complex", Justification = "A flat comparison-kind switch is a zero-allocation jump table.")]
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

    /// <summary>Reports SST1182 when a conditional expression yields only the boolean literals.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeConditionalBooleanLiteral(SyntaxNodeAnalysisContext context)
    {
        var conditional = (ConditionalExpressionSyntax)context.Node;
        var whenTrue = conditional.WhenTrue;
        var whenFalse = conditional.WhenFalse;

        // Flag only the 'true'/'false' pairing; 'c ? true : true' is a different (always-true) smell.
        var collapses = (whenTrue.IsKind(SyntaxKind.TrueLiteralExpression) && whenFalse.IsKind(SyntaxKind.FalseLiteralExpression))
            || (whenTrue.IsKind(SyntaxKind.FalseLiteralExpression) && whenFalse.IsKind(SyntaxKind.TrueLiteralExpression));
        if (!collapses)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoConditionalBooleanLiteral, conditional.GetLocation()));
    }

    /// <summary>Reports SST1183 when an interpolated string contains no interpolations.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInterpolatedString(SyntaxNodeAnalysisContext context)
    {
        var interpolated = (InterpolatedStringExpressionSyntax)context.Node;
        var contents = interpolated.Contents;
        for (var i = 0; i < contents.Count; i++)
        {
            if (contents[i].IsKind(SyntaxKind.Interpolation))
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantInterpolatedString, interpolated.GetLocation()));
    }

    /// <summary>Reports SST1184 when a verbatim string literal needs no verbatim quoting.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        var text = literal.Token.Text;

        // Only '@'-prefixed literals are verbatim; regular and raw string literals start differently.
        if (text.Length == 0 || text[0] != '@' || NeedsVerbatimQuoting(literal.Token.ValueText))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantVerbatimString, literal.GetLocation()));
    }

    /// <summary>Reports SST1185 for a self-recomputing assignment, or SST1187 for a chained assignment.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeSimpleAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // SST1187: the value of this assignment is itself an assignment ('a = b = c').
        if (assignment.Right.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoChainedAssignment, assignment.GetLocation()));
            return;
        }

        // SST1189: the assignment copies a side-effect-free target onto itself ('x = x').
        if (CompoundAssignmentOperators.IsSideEffectFreeTarget(assignment.Left)
            && SyntaxFactory.AreEquivalent(assignment.Left, assignment.Right))
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoSelfAssignment, assignment.GetLocation(), assignment.Left.ToString()));
            return;
        }

        // SST1185: the value recomputes the target ('x = x op y') and the target is side-effect-free.
        if (assignment.Right is not BinaryExpressionSyntax binary
            || !CompoundAssignmentOperators.TryMap(binary.Kind(), out _, out _, out var operatorText)
            || !CompoundAssignmentOperators.IsSideEffectFreeTarget(assignment.Left)
            || !SyntaxFactory.AreEquivalent(assignment.Left, binary.Left))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseCompoundAssignment, assignment.GetLocation(), operatorText));
    }

    /// <summary>Reports SST1186 when a non-null literal sits on the left of an equality comparison.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeComparison(SyntaxNodeAnalysisContext context)
    {
        var comparison = (BinaryExpressionSyntax)context.Node;

        // Null comparisons belong to the 'is null' rule, and two literals are a constant-folding concern.
        if (!IsReorderableLiteral(comparison.Left) || IsReorderableLiteral(comparison.Right))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.LiteralOnRightOfComparison, comparison.GetLocation()));
    }

    /// <summary>Reports SST1188 when <c>default(T)</c> sits in a target-typed position that accepts bare <c>default</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeDefaultExpression(SyntaxNodeAnalysisContext context)
    {
        var defaultExpression = (DefaultExpressionSyntax)context.Node;
        if (!IsTargetTypedDefaultPosition(defaultExpression))
        {
            return;
        }

        // Bare 'default' only keeps the meaning when the inferred type equals the spelled-out type.
        var info = context.SemanticModel.GetTypeInfo(defaultExpression, context.CancellationToken);
        if (info.Type is null
            || info.ConvertedType is null
            || !SymbolEqualityComparer.IncludeNullability.Equals(info.Type, info.ConvertedType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseDefaultLiteral, defaultExpression.GetLocation()));
    }

    /// <summary>Reports SST1190 when a prefix-negation operator is applied twice (<c>!!x</c>, <c>~~x</c>).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeDoubledNegation(SyntaxNodeAnalysisContext context)
    {
        var unary = (PrefixUnaryExpressionSyntax)context.Node;

        // Report once on the outermost operator of a run, so '!!!x' is flagged a single time.
        if (unary.Parent is PrefixUnaryExpressionSyntax outer && outer.IsKind(unary.Kind()))
        {
            return;
        }

        if (Unwrap(unary.Operand) is not PrefixUnaryExpressionSyntax inner || !inner.IsKind(unary.Kind()))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoDoubledNegation, unary.GetLocation(), unary.OperatorToken.ValueText));
    }

    /// <summary>Returns whether a string's text needs the verbatim form (has a backslash, quote, or line break).</summary>
    /// <param name="value">The decoded string value.</param>
    /// <returns><see langword="true"/> when a regular literal could not hold the same text unescaped.</returns>
    private static bool NeedsVerbatimQuoting(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is '\\' or '"' or '\n' or '\r')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an expression is a literal that may be moved to the right of a comparison.</summary>
    /// <param name="expression">The comparison operand.</param>
    /// <returns><see langword="true"/> for any literal other than <c>null</c>.</returns>
    private static bool IsReorderableLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax literal && !literal.IsKind(SyntaxKind.NullLiteralExpression);

    /// <summary>Returns whether a <c>default(T)</c> sits where the compiler supplies an unambiguous target type.</summary>
    /// <param name="defaultExpression">The default expression.</param>
    /// <returns><see langword="true"/> for a return, arrow body, assignment value, or non-<c>var</c> initializer.</returns>
    private static bool IsTargetTypedDefaultPosition(DefaultExpressionSyntax defaultExpression) => defaultExpression.Parent switch
    {
        ReturnStatementSyntax => true,
        ArrowExpressionClauseSyntax => true,
        AssignmentExpressionSyntax assignment => assignment.Right == defaultExpression,
        EqualsValueClauseSyntax equals => !IsVarLocalInitializer(equals),
        _ => false
    };

    /// <summary>Returns whether an initializer belongs to a <c>var</c> local, where bare <c>default</c> has no type.</summary>
    /// <param name="equals">The initializer clause.</param>
    /// <returns><see langword="true"/> when the initializer is for a <c>var</c>-typed local declaration.</returns>
    private static bool IsVarLocalInitializer(EqualsValueClauseSyntax equals)
        => equals.Parent is VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration }
            && declaration.Type.IsVar;

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
