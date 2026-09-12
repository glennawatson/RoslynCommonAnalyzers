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
    /// <summary>The sequence extension that re-types every element.</summary>
    private const string SequenceCastMethodName = "Cast";

    /// <summary>The sequence extension that keeps only the elements of a type.</summary>
    private const string SequenceFilterMethodName = "OfType";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(
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
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeInvertedBooleanCheck, SyntaxKind.LogicalNotExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAnonymousTypeMember, SyntaxKind.AnonymousObjectMemberDeclarator);
        context.RegisterSyntaxNodeAction(AnalyzeRedundantCast, SyntaxKind.CastExpression);
        context.RegisterSyntaxNodeAction(AnalyzeRedundantAsCast, SyntaxKind.AsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeRedundantSequenceCast, SyntaxKind.InvocationExpression);
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

    /// <summary>Returns whether a relational operand is nullable, floating-point, or conditional-access.</summary>
    /// <param name="operand">The operand to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when inverting the comparison would not preserve the result.</returns>
    /// <remarks>
    /// Shared with the guard-clause code fix, which has to answer the same question before it flips a
    /// relational operator, so the rule about what may be inverted lives in one place.
    /// </remarks>
    internal static bool IsUnsafeRelationalOperand(ExpressionSyntax operand, SemanticModel model, CancellationToken cancellationToken)
    {
        if (operand.IsKind(SyntaxKind.ConditionalAccessExpression))
        {
            return true;
        }

        var type = model.GetTypeInfo(operand, cancellationToken).Type;
        return type is not null && (IsFloatingPoint(type) || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);
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

        var operandInfo = context.SemanticModel.GetTypeInfo(cast.Expression, context.CancellationToken);
        if (operandInfo.Type is not { } operandType)
        {
            return;
        }

        var targetType = context.SemanticModel.GetTypeInfo(cast.Type, context.CancellationToken).Type;

        // The type syntax carries no flow state, so its NullableAnnotation is always None and comparing
        // it with the operand's would never match for a reference type. Compare the types themselves and
        // judge nullability separately, below.
        if (targetType is null || !SymbolEqualityComparer.Default.Equals(operandType, targetType))
        {
            return;
        }

        if (!KeepsNullState(operandInfo, cast.Type, targetType) || !TypeArgumentsAgreeOnNullability(operandType, targetType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantCast, cast.Type.GetLocation(), targetType.ToDisplayString()));
    }

    /// <summary>Reports SST1175 when an <c>as</c> tests for the type the operand already has.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeRedundantAsCast(SyntaxNodeAnalysisContext context)
    {
        var asExpression = (BinaryExpressionSyntax)context.Node;
        if (asExpression.Right is not TypeSyntax castType)
        {
            return;
        }

        var operand = Unwrap(asExpression.Left);
        if (operand.IsKind(SyntaxKind.DefaultLiteralExpression) || operand.IsKind(SyntaxKind.DefaultExpression))
        {
            return;
        }

        var operandInfo = context.SemanticModel.GetTypeInfo(asExpression.Left, context.CancellationToken);
        if (operandInfo.Type is not { } operandType
            || context.SemanticModel.GetTypeInfo(castType, context.CancellationToken).Type is not { } targetType
            || !SymbolEqualityComparer.Default.Equals(operandType, targetType)
            || !KeepsNullState(operandInfo, castType, targetType)
            || !TypeArgumentsAgreeOnNullability(operandType, targetType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantCast, castType.GetLocation(), targetType.ToDisplayString()));
    }

    /// <summary>Reports SST1175 when a sequence is re-typed to the element type it already has.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <remarks>
    /// <c>Cast&lt;T&gt;()</c> over an <c>IEnumerable&lt;T&gt;</c> yields the same sequence.
    /// <c>OfType&lt;T&gt;()</c> only does so when <c>T</c> cannot hold null — otherwise it is still
    /// dropping the null elements, which is a filter rather than a conversion.
    /// </remarks>
    private static void AnalyzeRedundantSequenceCast(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: SequenceCastMethodName or SequenceFilterMethodName } name })
        {
            return;
        }

        if (GetUnchangedElementType(context, invocation) is not { } requestedElement)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantCast, name.GetLocation(), requestedElement.ToDisplayString()));
    }

    /// <summary>Returns the element type a sequence call asks for when the source already yields it.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The sequence call.</param>
    /// <returns>The requested element type, or <see langword="null"/> when the call still does something.</returns>
    private static ITypeSymbol? GetUnchangedElementType(in SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsEnumerableExtension(method)
            || GetSequenceElementType(method.ReturnType) is not { } requestedElement
            || (string.Equals(method.Name, SequenceFilterMethodName, StringComparison.Ordinal) && CanHoldNull(requestedElement)))
        {
            return null;
        }

        var unchanged = GetSourceExpression(invocation, method) is { } source
            && GetSequenceElementType(context.SemanticModel.GetTypeInfo(source, context.CancellationToken).Type) is { } sourceElement
            && SymbolEqualityComparer.Default.Equals(sourceElement, requestedElement)
            && sourceElement.NullableAnnotation == requestedElement.NullableAnnotation;

        return unchanged ? requestedElement : null;
    }

    /// <summary>Returns whether a bound method is one of the sequence extensions on <c>IEnumerable</c>.</summary>
    /// <param name="method">The bound method.</param>
    /// <returns><see langword="true"/> for the framework's own sequence re-typing extensions.</returns>
    private static bool IsEnumerableExtension(IMethodSymbol method) =>
        method.IsExtensionMethod
            && method.ContainingType is { Name: "Enumerable", ContainingNamespace: { Name: "Linq", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } };

    /// <summary>Returns the expression carrying the sequence, whether the call is reduced or written out.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="method">The bound method.</param>
    /// <returns>The source sequence expression, or <see langword="null"/> when it cannot be read.</returns>
    private static ExpressionSyntax? GetSourceExpression(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (method.ReducedFrom is not null)
        {
            return invocation.Expression is MemberAccessExpressionSyntax memberAccess ? memberAccess.Expression : null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        return arguments.Count == 0 ? null : arguments[0].Expression;
    }

    /// <summary>Returns the element type a sequence yields, for an array or anything implementing the generic interface.</summary>
    /// <param name="sequence">The sequence type.</param>
    /// <returns>The element type, or <see langword="null"/> when it is absent or ambiguous.</returns>
    private static ITypeSymbol? GetSequenceElementType(ITypeSymbol? sequence)
    {
        if (sequence is IArrayTypeSymbol { Rank: 1 } array)
        {
            return array.ElementType;
        }

        if (sequence is not INamedTypeSymbol named)
        {
            return null;
        }

        if (named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            return named.TypeArguments[0];
        }

        // A type implementing the interface twice has no single element type, so it is left alone.
        ITypeSymbol? found = null;
        var interfaces = named.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (interfaces[i].OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                continue;
            }

            if (found is not null)
            {
                return null;
            }

            found = interfaces[i].TypeArguments[0];
        }

        return found;
    }

    /// <summary>Returns whether a type has <see langword="null"/> among its values.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for a reference type or a nullable value type.</returns>
    private static bool CanHoldNull(ITypeSymbol type) =>
        type.IsReferenceType || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    /// <summary>Returns whether removing the cast would leave the operand's null-state unchanged.</summary>
    /// <param name="operandInfo">The operand's type info, carrying its flow state.</param>
    /// <param name="castType">The cast's type syntax.</param>
    /// <param name="targetType">The cast's target type.</param>
    /// <returns><see langword="true"/> when the cast neither asserts nor relaxes nullability.</returns>
    /// <remarks>
    /// A cast is the usual way to move a value between null-states, and either direction is a reason to
    /// keep it: casting a maybe-null value to the plain type asserts it is not null, and casting a
    /// not-null value to the nullable type widens what an inferred local will hold.
    /// </remarks>
    private static bool KeepsNullState(in TypeInfo operandInfo, TypeSyntax castType, ITypeSymbol targetType)
    {
        var admitsNull = castType.IsKind(SyntaxKind.NullableType)
            || targetType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

        return operandInfo.Nullability.FlowState switch
        {
            NullableFlowState.MaybeNull => admitsNull,
            NullableFlowState.NotNull => !admitsNull,
            _ => true,
        };
    }

    /// <summary>Returns whether two equal types annotate their type arguments the same way, to any depth.</summary>
    /// <param name="operandType">The operand's type.</param>
    /// <param name="targetType">The cast's target type.</param>
    /// <returns><see langword="true"/> when every corresponding type argument shares an annotation.</returns>
    /// <remarks>
    /// Type equality ignores the annotations inside a generic type, so <c>List&lt;string&gt;</c> and
    /// <c>List&lt;string?&gt;</c> compare equal. The cast between them is the one thing telling the
    /// compiler which element null-state to use, so it has to be kept.
    /// </remarks>
    private static bool TypeArgumentsAgreeOnNullability(ITypeSymbol operandType, ITypeSymbol targetType)
    {
        if (operandType is not INamedTypeSymbol { TypeArguments: { Length: > 0 } operandArguments }
            || targetType is not INamedTypeSymbol { TypeArguments: var targetArguments }
            || operandArguments.Length != targetArguments.Length)
        {
            return true;
        }

        for (var i = 0; i < operandArguments.Length; i++)
        {
            if (operandArguments[i].NullableAnnotation != targetArguments[i].NullableAnnotation
                || !TypeArgumentsAgreeOnNullability(operandArguments[i], targetArguments[i]))
            {
                return false;
            }
        }

        return true;
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

        // Inside an object or 'with' initializer the left side names a member of the object being built,
        // not something readable in the enclosing scope: 'new() { Name = Name }' copies this instance's
        // Name onto the new one, so the two identical-looking sides are different members of different
        // objects. Neither rule below applies — and a compound assignment is not even legal in that
        // position, so rewriting to '+=' would not compile.
        if (IsMemberInitializer(assignment))
        {
            return;
        }

        // SST1189: a tuple assignment copies element-wise, so each pair is judged on its own.
        if (TryReportTupleSelfAssignment(context, assignment))
        {
            return;
        }

        // SST1189: the assignment copies a side-effect-free target onto itself ('x = x').
        if (IsSelfAssignment(context, assignment))
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

    /// <summary>Returns whether an assignment copies a side-effect-free target onto itself.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="assignment">The assignment expression.</param>
    /// <returns><see langword="true"/> when both sides are the same thing.</returns>
    private static bool IsSelfAssignment(in SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignment) =>
        CompoundAssignmentOperators.IsSideEffectFreeTarget(assignment.Left)
            && (SyntaxFactory.AreEquivalent(assignment.Left, assignment.Right)
                || NamesSameMemberThroughThis(context, assignment.Left, assignment.Right));

    /// <summary>Reports SST1189 for every element of a tuple assignment that copies a value onto itself.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="assignment">The assignment expression.</param>
    /// <returns><see langword="true"/> when the assignment is a tuple-to-tuple one, which is reported element-wise.</returns>
    /// <remarks>
    /// A tuple assignment is redundant per element rather than as a whole: <c>(x, y) = (x, z)</c> does real
    /// work for <c>y</c> and none for <c>x</c>, so the element is the unit of both the report and the
    /// judgement. The location is the element rather than the statement, which is also what stops the code
    /// fix from deleting an assignment that still moves a value.
    /// </remarks>
    private static bool TryReportTupleSelfAssignment(in SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignment)
    {
        if (assignment.Left is not TupleExpressionSyntax left || assignment.Right is not TupleExpressionSyntax right)
        {
            return false;
        }

        var targets = left.Arguments;
        var values = right.Arguments;
        if (targets.Count != values.Count)
        {
            return false;
        }

        for (var i = 0; i < targets.Count; i++)
        {
            var target = targets[i].Expression;
            if (!CompoundAssignmentOperators.IsSideEffectFreeTarget(target)
                || !SyntaxFactory.AreEquivalent(target, values[i].Expression))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoSelfAssignment, target.GetLocation(), target.ToString()));
        }

        return true;
    }

    /// <summary>Returns whether the two sides name the same member, one of them qualified with <c>this</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="left">The assignment target.</param>
    /// <param name="right">The assigned value.</param>
    /// <returns><see langword="true"/> when both sides bind to the same member.</returns>
    /// <remarks>
    /// <c>this.value = value</c> reads like the ordinary constructor assignment, and whether it is one depends
    /// entirely on what the unqualified name binds to: a parameter that shadows the field makes it real work,
    /// and the field itself makes it a no-op. Only the symbols can tell those apart. The names are compared
    /// first so nothing binds unless the two sides are at least spelled the same, and the both-qualified and
    /// both-unqualified spellings are left to the syntactic test that already covers them.
    /// </remarks>
    private static bool NamesSameMemberThroughThis(in SyntaxNodeAnalysisContext context, ExpressionSyntax left, ExpressionSyntax right)
    {
        if (!TryGetMemberName(left, out var leftName, out var leftIsQualified)
            || !TryGetMemberName(right, out var rightName, out var rightIsQualified)
            || leftIsQualified == rightIsQualified
            || !string.Equals(leftName, rightName, StringComparison.Ordinal))
        {
            return false;
        }

        var target = context.SemanticModel.GetSymbolInfo(left, context.CancellationToken).Symbol;
        return target is not null
            && SymbolEqualityComparer.Default.Equals(target, context.SemanticModel.GetSymbolInfo(right, context.CancellationToken).Symbol);
    }

    /// <summary>Reads the member name an operand spells, and whether it was reached through <c>this</c>.</summary>
    /// <param name="expression">The operand.</param>
    /// <param name="name">The member name.</param>
    /// <param name="isQualified">Whether the name was qualified with <c>this</c>.</param>
    /// <returns><see langword="true"/> for a bare name or a <c>this</c>-qualified one.</returns>
    private static bool TryGetMemberName(ExpressionSyntax expression, out string name, out bool isQualified)
    {
        switch (expression)
        {
            case IdentifierNameSyntax identifier:
            {
                name = identifier.Identifier.ValueText;
                isQualified = false;
                return true;
            }

            case MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax member }:
            {
                name = member.Identifier.ValueText;
                isQualified = true;
                return true;
            }

            default:
            {
                name = string.Empty;
                isQualified = false;
                return false;
            }
        }
    }

    /// <summary>Returns whether an assignment sets a member of the object being built rather than one in scope.</summary>
    /// <param name="assignment">The assignment expression.</param>
    /// <returns><see langword="true"/> for an assignment directly inside an object or <c>with</c> initializer.</returns>
    private static bool IsMemberInitializer(AssignmentExpressionSyntax assignment) =>
        assignment.Parent is InitializerExpressionSyntax initializer
            && (initializer.IsKind(SyntaxKind.ObjectInitializerExpression) || initializer.IsKind(SyntaxKind.WithInitializerExpression));

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
        // The bare 'default' literal is a C# 7.1 feature; below that the fix would not compile.
        if (context.Node.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp7_1 })
        {
            return;
        }

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
    private static bool IsReorderableLiteral(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal && !literal.IsKind(SyntaxKind.NullLiteralExpression);

    /// <summary>Returns whether a <c>default(T)</c> sits where the compiler supplies an unambiguous target type.</summary>
    /// <param name="defaultExpression">The default expression.</param>
    /// <returns><see langword="true"/> for a return, arrow body, assignment value, or non-<c>var</c> initializer.</returns>
    private static bool IsTargetTypedDefaultPosition(DefaultExpressionSyntax defaultExpression) => defaultExpression.Parent switch
    {
        ReturnStatementSyntax or ArrowExpressionClauseSyntax => true,
        AssignmentExpressionSyntax assignment => assignment.Right == defaultExpression,
        EqualsValueClauseSyntax equals => !IsVarLocalInitializer(equals),
        _ => false
    };

    /// <summary>Returns whether an initializer belongs to a <c>var</c> local, where bare <c>default</c> has no type.</summary>
    /// <param name="equals">The initializer clause.</param>
    /// <returns><see langword="true"/> when the initializer is for a <c>var</c>-typed local declaration.</returns>
    private static bool IsVarLocalInitializer(EqualsValueClauseSyntax equals) =>
        equals.Parent is VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration }
            && declaration.Type.IsVar;

    /// <summary>Returns whether a type is a floating-point type that has a <c>NaN</c> value.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for <see cref="float"/>, <see cref="double"/>, <c>Half</c>, or <c>NFloat</c>.</returns>
    private static bool IsFloatingPoint(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Single or SpecialType.System_Double
            || type.Name is "Half" or "NFloat";
}
