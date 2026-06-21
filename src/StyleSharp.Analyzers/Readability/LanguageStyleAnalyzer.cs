// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Groups safe C# language-style preferences whose common paths can be checked syntactically, with
/// semantic binding only after a candidate shape is found.
/// </summary>
/// <remarks>
/// Reports SST1193-SST1199 for object/collection initializers, null simplifications,
/// conditional return/assignment expressions, and <c>nameof</c> type-name expressions.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LanguageStyleAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ReadabilityRules.UseObjectInitializer,
        ReadabilityRules.UseCollectionInitializer,
        ReadabilityRules.UseNullCoalescingExpression,
        ReadabilityRules.UseNullPropagation,
        ReadabilityRules.UseConditionalExpressionForReturn,
        ReadabilityRules.UseConditionalExpressionForAssignment,
        ReadabilityRules.UseNameofType);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
        context.RegisterSyntaxNodeAction(AnalyzeConditionalExpression, SyntaxKind.ConditionalExpression);
        context.RegisterSyntaxNodeAction(AnalyzeIfStatement, SyntaxKind.IfStatement);
        context.RegisterSyntaxNodeAction(AnalyzeTypeofName, SyntaxKind.SimpleMemberAccessExpression);
    }

    /// <summary>Reports initializer opportunities after local object construction.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context)
    {
        var declarationStatement = (LocalDeclarationStatementSyntax)context.Node;
        if (!TryGetEmptyLocalCreation(declarationStatement, out var objectCreation, out var variableName, out var block))
        {
            return;
        }

        var next = NextStatement(block, declarationStatement);
        if (next is null)
        {
            return;
        }

        if (IsMemberAssignment(next, variableName, context.SemanticModel, context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseObjectInitializer, objectCreation.GetLocation(), variableName));
            return;
        }

        if (!IsAddCall(next, variableName, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (!IsCollectionType(context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseCollectionInitializer, objectCreation.GetLocation(), variableName));
    }

    /// <summary>Reports null-coalescing and null-propagation conditional-expression shapes.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeConditionalExpression(SyntaxNodeAnalysisContext context)
    {
        var conditional = (ConditionalExpressionSyntax)context.Node;
        var parts = TryGetNullConditionalParts(conditional, context.SemanticModel, context.CancellationToken);
        if (parts is null)
        {
            return;
        }

        var value = parts.Value;
        if (IsSameStableSymbol(value.Operand, value.WhenNotNull, value.OperandSymbol, context.SemanticModel, context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseNullCoalescingExpression, conditional.GetLocation()));
            return;
        }

        if (!value.Fallback.IsKind(SyntaxKind.NullLiteralExpression)
            || !IsMemberAccessOnReceiver(value.WhenNotNull, value.Operand, value.OperandSymbol, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseNullPropagation, conditional.GetLocation()));
    }

    /// <summary>Reports conditional return/assignment statement shapes.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context)
    {
        var ifStatement = (IfStatementSyntax)context.Node;
        if (IsConditionalReturnCandidate(ifStatement))
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseConditionalExpressionForReturn, ifStatement.IfKeyword.GetLocation()));
            return;
        }

        if (!IsConditionalAssignmentCandidate(ifStatement, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseConditionalExpressionForAssignment, ifStatement.IfKeyword.GetLocation()));
    }

    /// <summary>Reports <c>typeof(T).Name</c> when <c>T</c> is a non-generic type syntax that <c>nameof</c> can name.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeTypeofName(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        if (memberAccess.Name.Identifier.ValueText != "Name"
            || memberAccess.Expression is not TypeOfExpressionSyntax { Type: { } type }
            || ContainsGenericSyntax(type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseNameofType, memberAccess.GetLocation(), type.ToString()));
    }

    /// <summary>Returns whether a local declaration creates an empty object that can take an initializer.</summary>
    /// <param name="declarationStatement">The local declaration statement.</param>
    /// <param name="objectCreation">The empty object creation expression.</param>
    /// <param name="variableName">The declared local variable name.</param>
    /// <param name="block">The containing block.</param>
    /// <returns><see langword="true"/> when the declaration is a single empty local object creation.</returns>
    private static bool TryGetEmptyLocalCreation(
        LocalDeclarationStatementSyntax declarationStatement,
        out ObjectCreationExpressionSyntax objectCreation,
        out string variableName,
        out BlockSyntax block)
    {
        objectCreation = null!;
        variableName = string.Empty;
        block = null!;

        if (declarationStatement.Declaration.Variables.Count != 1
            || declarationStatement.Declaration.Variables[0] is not { Initializer.Value: ObjectCreationExpressionSyntax created } variable
            || created.Initializer is not null
            || created.ArgumentList is null
            || created.ArgumentList.Arguments.Count != 0
            || declarationStatement.Parent is not BlockSyntax parentBlock)
        {
            return false;
        }

        objectCreation = created;
        variableName = variable.Identifier.ValueText;
        block = parentBlock;
        return true;
    }

    /// <summary>Returns the statement immediately after the supplied statement in a block.</summary>
    /// <param name="block">The containing block.</param>
    /// <param name="statement">The current statement.</param>
    /// <returns>The next statement, or <see langword="null"/>.</returns>
    private static StatementSyntax? NextStatement(BlockSyntax block, StatementSyntax statement)
    {
        var statements = block.Statements;
        for (var i = 0; i < statements.Count - 1; i++)
        {
            if (statements[i].Span == statement.Span)
            {
                return statements[i + 1];
            }
        }

        return null;
    }

    /// <summary>Returns whether a statement assigns a member of the named local.</summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <param name="variableName">The local variable name.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the statement assigns a member of the local.</returns>
    private static bool IsMemberAssignment(StatementSyntax statement, string variableName, SemanticModel model, CancellationToken cancellationToken)
    {
        if (statement is not ExpressionStatementSyntax
            {
                Expression: AssignmentExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: MemberAccessExpressionSyntax memberAccess
                }
            })
        {
            return false;
        }

        return IsIdentifier(memberAccess.Expression, variableName)
            && model.GetSymbolInfo(memberAccess.Name, cancellationToken).Symbol is IPropertySymbol or IFieldSymbol;
    }

    /// <summary>Returns whether a statement invokes <c>Add</c> on the named local.</summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <param name="variableName">The local variable name.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the statement calls <c>Add</c> on the local.</returns>
    private static bool IsAddCall(StatementSyntax statement, string variableName, SemanticModel model, CancellationToken cancellationToken)
    {
        if (statement is not ExpressionStatementSyntax expressionStatement
            || expressionStatement.Expression is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Expression is not IdentifierNameSyntax receiver
            || memberAccess.Name.Identifier.ValueText != "Add"
            || invocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        return receiver.Identifier.ValueText == variableName
            && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { Name: "Add" };
    }

    /// <summary>Returns whether a type supports collection initializer syntax.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> when the type implements <c>IEnumerable</c>.</returns>
    private static bool IsCollectionType(ITypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (IsNonGenericEnumerable(current))
            {
                return true;
            }

            foreach (var @interface in current.Interfaces)
            {
                if (IsNonGenericEnumerable(@interface))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a type symbol is <c>System.Collections.IEnumerable</c>.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> when the type is non-generic <c>IEnumerable</c>.</returns>
    private static bool IsNonGenericEnumerable(ITypeSymbol type)
        => type.SpecialType == SpecialType.System_Collections_IEnumerable;

    /// <summary>Extracts <c>x == null ? fallback : whenNotNull</c> and <c>x != null ? whenNotNull : fallback</c> parts.</summary>
    /// <param name="conditional">The conditional expression to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns>The extracted parts, or <see langword="null"/> when the expression is not a safe null conditional.</returns>
    private static NullConditionalParts? TryGetNullConditionalParts(
        ConditionalExpressionSyntax conditional,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (ExpressionSimplificationAnalyzer.Unwrap(conditional.Condition) is not BinaryExpressionSyntax binary
            || !TryGetNullComparison(binary, model, cancellationToken, out var operand, out var operandSymbol))
        {
            return null;
        }

        return binary.IsKind(SyntaxKind.EqualsExpression)
            ? new NullConditionalParts(operand, conditional.WhenTrue, conditional.WhenFalse, operandSymbol)
            : new NullConditionalParts(operand, conditional.WhenFalse, conditional.WhenTrue, operandSymbol);
    }

    /// <summary>Returns the non-null operand of a null equality comparison.</summary>
    /// <param name="binary">The binary expression to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="operand">The non-null operand.</param>
    /// <param name="operandSymbol">The symbol read by the operand.</param>
    /// <returns><see langword="true"/> when the comparison reads a stable operand against <see langword="null"/>.</returns>
    private static bool TryGetNullComparison(
        BinaryExpressionSyntax binary,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ExpressionSyntax operand,
        out ISymbol operandSymbol)
    {
        operand = null!;
        operandSymbol = null!;
        if (!binary.IsKind(SyntaxKind.EqualsExpression) && !binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            return false;
        }

        var leftNull = binary.Left.IsKind(SyntaxKind.NullLiteralExpression);
        var rightNull = binary.Right.IsKind(SyntaxKind.NullLiteralExpression);
        if (leftNull == rightNull)
        {
            return false;
        }

        operand = leftNull ? binary.Right : binary.Left;
        return TryGetStableReadableExpression(operand, model, cancellationToken, out operandSymbol);
    }

    /// <summary>Returns whether the member access receiver matches the guarded expression.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="receiver">The expected receiver expression.</param>
    /// <param name="receiverSymbol">The expected receiver symbol.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the expression is a member access on the receiver.</returns>
    private static bool IsMemberAccessOnReceiver(
        ExpressionSyntax expression,
        ExpressionSyntax receiver,
        ISymbol receiverSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
        => expression is MemberAccessExpressionSyntax memberAccess
        && IsSameStableSymbol(receiver, memberAccess.Expression, receiverSymbol, model, cancellationToken);

    /// <summary>Returns whether two expressions bind to the same stable symbol.</summary>
    /// <param name="left">The first expression.</param>
    /// <param name="right">The second expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when both expressions bind to the same stable symbol.</returns>
    private static bool IsSameStableSymbol(ExpressionSyntax left, ExpressionSyntax right, SemanticModel model, CancellationToken cancellationToken)
    {
        var unwrappedLeft = ExpressionSimplificationAnalyzer.Unwrap(left);
        var unwrappedRight = ExpressionSimplificationAnalyzer.Unwrap(right);
        var leftSymbol = model.GetSymbolInfo(unwrappedLeft, cancellationToken).Symbol;
        return IsStableSymbol(leftSymbol)
            && (IsSameIdentifierRead(unwrappedLeft, unwrappedRight)
                || SymbolEqualityComparer.Default.Equals(leftSymbol, model.GetSymbolInfo(unwrappedRight, cancellationToken).Symbol));
    }

    /// <summary>Returns whether a candidate expression binds to an already-known stable symbol.</summary>
    /// <param name="knownExpression">The expression used to obtain <paramref name="knownSymbol"/>.</param>
    /// <param name="candidate">The expression to compare.</param>
    /// <param name="knownSymbol">The known stable symbol.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the candidate reads the same symbol.</returns>
    private static bool IsSameStableSymbol(
        ExpressionSyntax knownExpression,
        ExpressionSyntax candidate,
        ISymbol knownSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var unwrappedKnown = ExpressionSimplificationAnalyzer.Unwrap(knownExpression);
        var unwrappedCandidate = ExpressionSimplificationAnalyzer.Unwrap(candidate);
        return IsSameIdentifierRead(unwrappedKnown, unwrappedCandidate)
            || SymbolEqualityComparer.Default.Equals(knownSymbol, model.GetSymbolInfo(unwrappedCandidate, cancellationToken).Symbol);
    }

    /// <summary>Returns whether an expression is a local or parameter read.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="symbol">The stable symbol read by the expression.</param>
    /// <returns><see langword="true"/> when the expression is a stable read.</returns>
    private static bool TryGetStableReadableExpression(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken, out ISymbol symbol)
    {
        symbol = model.GetSymbolInfo(ExpressionSimplificationAnalyzer.Unwrap(expression), cancellationToken).Symbol!;
        return IsStableSymbol(symbol);
    }

    /// <summary>Returns whether repeated reads avoid user code.</summary>
    /// <param name="symbol">The symbol read by an expression.</param>
    /// <returns><see langword="true"/> for locals and parameters.</returns>
    private static bool IsStableSymbol(ISymbol? symbol)
        => symbol is ILocalSymbol or IParameterSymbol;

    /// <summary>Returns whether two expressions are the same identifier token.</summary>
    /// <param name="left">The first expression.</param>
    /// <param name="right">The second expression.</param>
    /// <returns><see langword="true"/> when both expressions read the same identifier text.</returns>
    private static bool IsSameIdentifierRead(ExpressionSyntax left, ExpressionSyntax right)
        => left is IdentifierNameSyntax leftIdentifier
        && right is IdentifierNameSyntax rightIdentifier
        && leftIdentifier.Identifier.ValueText == rightIdentifier.Identifier.ValueText;

    /// <summary>Returns whether an if statement can become a conditional return.</summary>
    /// <param name="ifStatement">The if statement to inspect.</param>
    /// <returns><see langword="true"/> when the if and following statement both return values.</returns>
    private static bool IsConditionalReturnCandidate(IfStatementSyntax ifStatement)
    {
        if (GetEmbeddedReturn(ifStatement.Statement) is not { } whenTrue
            || ifStatement.Else?.Statement is not null
            || ifStatement.Parent is not BlockSyntax block
            || NextStatement(block, ifStatement) is not { } next)
        {
            return false;
        }

        return GetEmbeddedReturn(next) is { } whenFalse
            && !WouldNestConditionalExpression(ifStatement.Condition, whenTrue, whenFalse);
    }

    /// <summary>Returns whether a conditional rewrite would create nested conditional expressions.</summary>
    /// <param name="condition">The condition expression.</param>
    /// <param name="whenTrue">The expression used for the true branch.</param>
    /// <param name="whenFalse">The expression used for the false branch.</param>
    /// <returns><see langword="true"/> when the replacement would nest a conditional expression.</returns>
    private static bool WouldNestConditionalExpression(ExpressionSyntax condition, ExpressionSyntax whenTrue, ExpressionSyntax whenFalse)
        => ContainsConditionalExpression(condition)
            || ContainsConditionalExpression(whenTrue)
            || ContainsConditionalExpression(whenFalse);

    /// <summary>Returns whether an expression contains a conditional expression.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> when a conditional expression is present.</returns>
    private static bool ContainsConditionalExpression(ExpressionSyntax expression)
    {
        if (expression is ConditionalExpressionSyntax)
        {
            return true;
        }

        foreach (var node in expression.DescendantNodes(static node => node is not ConditionalExpressionSyntax))
        {
            if (node is ConditionalExpressionSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an if statement can become a conditional assignment.</summary>
    /// <param name="ifStatement">The if statement to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when both branches assign the same stable target.</returns>
    private static bool IsConditionalAssignmentCandidate(IfStatementSyntax ifStatement, SemanticModel model, CancellationToken cancellationToken)
    {
        if (ifStatement.Else?.Statement is null)
        {
            return false;
        }

        var whenTrueTarget = GetEmbeddedAssignmentTarget(ifStatement.Statement);
        var whenFalseTarget = GetEmbeddedAssignmentTarget(ifStatement.Else.Statement);
        return whenTrueTarget is not null
            && whenFalseTarget is not null
            && IsSameStableSymbol(whenTrueTarget, whenFalseTarget, model, cancellationToken);
    }

    /// <summary>Returns a return expression from a statement or single-statement block.</summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <returns>The returned expression, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? GetEmbeddedReturn(StatementSyntax statement)
    {
        if (statement is ReturnStatementSyntax { Expression: { } expression })
        {
            return expression;
        }

        return statement is BlockSyntax { Statements.Count: 1 } block
            && block.Statements[0] is ReturnStatementSyntax { Expression: { } blockExpression }
            ? blockExpression
            : null;
    }

    /// <summary>Returns whether a statement is a simple assignment expression statement.</summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <returns>The assignment target, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? GetEmbeddedAssignmentTarget(StatementSyntax statement)
    {
        ExpressionSyntax? expression = null;
        if (statement is ExpressionStatementSyntax expressionStatement)
        {
            expression = expressionStatement.Expression;
        }
        else if (statement is BlockSyntax { Statements.Count: 1 } block
            && block.Statements[0] is ExpressionStatementSyntax blockStatement)
        {
            expression = blockStatement.Expression;
        }

        return expression is AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression, Left: { } left } ? left : null;
    }

    /// <summary>Returns whether the expression is an identifier with the expected text.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="value">The expected identifier text.</param>
    /// <returns><see langword="true"/> when the expression is the expected identifier.</returns>
    private static bool IsIdentifier(ExpressionSyntax expression, string value)
        => expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == value;

    /// <summary>Returns whether the type syntax contains generic syntax unsupported by <c>nameof</c>.</summary>
    /// <param name="type">The type syntax to inspect.</param>
    /// <returns><see langword="true"/> when the type syntax contains a generic name.</returns>
    private static bool ContainsGenericSyntax(TypeSyntax type)
    {
        if (type is GenericNameSyntax)
        {
            return true;
        }

        foreach (var node in type.DescendantNodes(static node => node is not GenericNameSyntax))
        {
            if (node is GenericNameSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Parts of a null-conditional expression shape.</summary>
    /// <param name="Operand">The expression checked against <c>null</c>.</param>
    /// <param name="Fallback">The expression used when the operand is <c>null</c>.</param>
    /// <param name="WhenNotNull">The expression used when the operand is not <c>null</c>.</param>
    /// <param name="OperandSymbol">The stable symbol read by <paramref name="Operand"/>.</param>
    private readonly record struct NullConditionalParts(ExpressionSyntax Operand, ExpressionSyntax Fallback, ExpressionSyntax WhenNotNull, ISymbol OperandSymbol);
}
