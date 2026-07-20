// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a user-defined <c>operator ==</c> declared on a mutable reference type (SST2464): a <c>class</c>
/// that still exposes a settable field or property. Value-based equality on state that can change is a
/// foot-gun — once an instance is used as a <c>Dictionary</c> or <c>HashSet</c> key and then mutated, its
/// hash no longer matches the bucket it was stored in, so it can never be found again and can corrupt
/// neighbouring entries.
/// </summary>
/// <remarks>
/// <para>
/// The clean path is syntactic: the node must be an <c>operator ==</c> declaration whose containing type is
/// a plain <c>class</c> (a <c>struct</c>, a <c>record</c>, and a <c>record struct</c> are a different syntax
/// node and never reach the bind). Only then is the type bound, to confirm it is a class that is mutable —
/// it declares at least one instance field that is not <c>readonly</c>/<c>const</c>, or an instance property
/// with a <c>set</c> accessor that is not <c>init</c>-only. An immutable class — every field <c>readonly</c>,
/// every property get-only or <c>init</c>-only — is left silent. Binding also merges partial declarations, so
/// state split across files is seen.
/// </para>
/// <para>
/// Only <c>operator ==</c> is reported, once; the paired <c>operator !=</c> is left alone so a single mutable
/// class yields a single diagnostic. Whether the type also overrides <c>Equals</c>/<c>GetHashCode</c> is
/// irrelevant here — the defect is value equality on mutable state, not a missing member.
/// </para>
/// <para>
/// An operator that is really identity equality is exempt: a body of <c>ReferenceEquals(left, right)</c> or
/// <c>(object)left == (object)right</c> compares the operands by reference, so the result never changes when
/// the state changes and the hash-key hazard the rule warns about cannot arise.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2464EqualityOperatorOnMutableClassAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.EqualityOperatorOnMutableClass);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeOperator, SyntaxKind.OperatorDeclaration);
    }

    /// <summary>Reports one <c>operator ==</c> declared on a mutable class.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeOperator(SyntaxNodeAnalysisContext context)
    {
        var operatorDeclaration = (OperatorDeclarationSyntax)context.Node;
        if (!operatorDeclaration.OperatorToken.IsKind(SyntaxKind.EqualsEqualsToken)
            || operatorDeclaration.Parent is not ClassDeclarationSyntax classDeclaration)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken) is not { TypeKind: TypeKind.Class, IsRecord: false } type
            || !IsMutable(type)
            || IsReferenceEquality(operatorDeclaration, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.EqualityOperatorOnMutableClass,
            operatorDeclaration.OperatorToken.GetLocation(),
            type.Name));
    }

    /// <summary>Returns whether a type declares any settable instance state.</summary>
    /// <param name="type">The containing class symbol (partial declarations already merged).</param>
    /// <returns><see langword="true"/> when the class has a non-readonly instance field or a non-init settable property.</returns>
    private static bool IsMutable(INamedTypeSymbol type)
    {
        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (member.IsStatic)
            {
                continue;
            }

            switch (member)
            {
                case IFieldSymbol { IsImplicitlyDeclared: false, IsConst: false, IsReadOnly: false }:
                case IPropertySymbol { SetMethod.IsInitOnly: false }:
                    return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an <c>operator ==</c> body is identity equality of its two operands.</summary>
    /// <param name="operatorDeclaration">The operator declaration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> for a body of <c>ReferenceEquals(left, right)</c> or <c>(object)left == (object)right</c>.</returns>
    private static bool IsReferenceEquality(OperatorDeclarationSyntax operatorDeclaration, SemanticModel model, CancellationToken cancellationToken)
    {
        var parameters = operatorDeclaration.ParameterList.Parameters;
        if (parameters.Count != 2 || GetBodyExpression(operatorDeclaration) is not { } body)
        {
            return false;
        }

        var left = parameters[0].Identifier.ValueText;
        var right = parameters[1].Identifier.ValueText;

        return Unwrap(body) switch
        {
            InvocationExpressionSyntax invocation => IsReferenceEqualsCall(invocation, left, right, model, cancellationToken),
            BinaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.EqualsEqualsToken } binary =>
                IsObjectCastOfParameter(binary.Left, left, right) && IsObjectCastOfParameter(binary.Right, left, right),
            _ => false,
        };
    }

    /// <summary>Returns the single expression an operator body evaluates to, or <see langword="null"/> when it has none.</summary>
    /// <param name="operatorDeclaration">The operator declaration.</param>
    /// <returns>The expression-body expression or the sole <c>return</c>'s expression, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? GetBodyExpression(OperatorDeclarationSyntax operatorDeclaration)
    {
        if (operatorDeclaration.ExpressionBody is { } arrow)
        {
            return arrow.Expression;
        }

        return operatorDeclaration.Body is { } block
            && block.Statements.Count == 1
            && block.Statements[0] is ReturnStatementSyntax { Expression: { } returned }
            ? returned
            : null;
    }

    /// <summary>Strips redundant parentheses from an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>Returns whether an invocation is <c>object.ReferenceEquals</c> applied to the two operands.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="left">The first operand's name.</param>
    /// <param name="right">The second operand's name.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the call is <c>ReferenceEquals(left, right)</c> in either argument order.</returns>
    private static bool IsReferenceEqualsCall(InvocationExpressionSyntax invocation, string left, string right, SemanticModel model, CancellationToken cancellationToken)
    {
        var arguments = invocation.ArgumentList.Arguments;
        return arguments.Count == 2
            && ArgumentsNameBothParameters(arguments[0].Expression, arguments[1].Expression, left, right)
            && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { Name: "ReferenceEquals", ContainingType.SpecialType: SpecialType.System_Object };
    }

    /// <summary>Returns whether two expressions are the two operand identifiers, in either order.</summary>
    /// <param name="first">The first argument expression.</param>
    /// <param name="second">The second argument expression.</param>
    /// <param name="left">The first operand's name.</param>
    /// <param name="right">The second operand's name.</param>
    /// <returns><see langword="true"/> when one names <paramref name="left"/> and the other <paramref name="right"/>.</returns>
    private static bool ArgumentsNameBothParameters(ExpressionSyntax first, ExpressionSyntax second, string left, string right)
    {
        var a = (Unwrap(first) as IdentifierNameSyntax)?.Identifier.ValueText;
        var b = (Unwrap(second) as IdentifierNameSyntax)?.Identifier.ValueText;
        return (a == left && b == right) || (a == right && b == left);
    }

    /// <summary>Returns whether an expression is an <c>(object)</c> cast of one of the two operands.</summary>
    /// <param name="expression">The operand expression.</param>
    /// <param name="left">The first operand's name.</param>
    /// <param name="right">The second operand's name.</param>
    /// <returns><see langword="true"/> when the expression casts one operand to <c>object</c>.</returns>
    private static bool IsObjectCastOfParameter(ExpressionSyntax expression, string left, string right)
    {
        if (Unwrap(expression) is not CastExpressionSyntax { Type: PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword } } cast)
        {
            return false;
        }

        var name = (Unwrap(cast.Expression) as IdentifierNameSyntax)?.Identifier.ValueText;
        return name == left || name == right;
    }
}
