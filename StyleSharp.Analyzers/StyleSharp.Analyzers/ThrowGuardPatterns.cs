// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Purely syntactic matchers for the hand-written argument-guard idioms that the
/// modern runtime throw-helpers replace. Shared by <see cref="ArgumentGuardAnalyzer"/>
/// and its code fix so detection and rewriting never drift apart. Everything here is
/// structural (no semantic model) to keep the analyzer's hot path allocation-free.
/// </summary>
internal static class ThrowGuardPatterns
{
    /// <summary>
    /// Matches <c>if (E is null) throw new ArgumentNullException(...);</c> (and the
    /// <c>== null</c> form), where the constructor takes no argument or a single
    /// <c>nameof(E)</c>/string argument naming the checked expression. The single-argument
    /// constraint matters because <see cref="System.ArgumentNullException"/>'s first
    /// constructor argument is the parameter name, which is exactly what
    /// <c>ThrowIfNull</c> infers — so the rewrite never loses a custom message.
    /// </summary>
    /// <param name="ifStatement">The candidate if statement.</param>
    /// <param name="checkedExpression">The null-checked expression when matched.</param>
    /// <returns><see langword="true"/> when the statement is a replaceable null guard.</returns>
    public static bool TryMatchArgumentNull(IfStatementSyntax ifStatement, out ExpressionSyntax? checkedExpression)
    {
        checkedExpression = null;
        if (ifStatement.Else is not null
            || !TryGetThrownCreation(ifStatement.Statement, out var creation)
            || !TryGetNullCheckOperand(ifStatement.Condition, out var operand)
            || SimpleTypeName(creation!.Type) != "ArgumentNullException"
            || !ParamNameArgumentMatches(creation.ArgumentList, operand!))
        {
            return false;
        }

        checkedExpression = operand;
        return true;
    }

    /// <summary>Returns the thrown <c>new T(...)</c> creation when the body is a single such throw.</summary>
    /// <param name="statement">The if body (a throw statement or a block wrapping one).</param>
    /// <param name="creation">The object-creation expression being thrown when matched.</param>
    /// <returns><see langword="true"/> when the body throws a single newly-created exception.</returns>
    private static bool TryGetThrownCreation(StatementSyntax statement, out ObjectCreationExpressionSyntax? creation)
    {
        var throwStatement = statement switch
        {
            ThrowStatementSyntax direct => direct,
            BlockSyntax block when block.Statements.Count == 1 && block.Statements[0] is ThrowStatementSyntax single => single,
            _ => null,
        };

        creation = throwStatement?.Expression as ObjectCreationExpressionSyntax;
        return creation is not null;
    }

    /// <summary>Returns the operand of an <c>E is null</c> or <c>E == null</c> / <c>null == E</c> condition.</summary>
    /// <param name="condition">The if condition.</param>
    /// <param name="operand">The non-null operand when matched.</param>
    /// <returns><see langword="true"/> when the condition is a null check.</returns>
    private static bool TryGetNullCheckOperand(ExpressionSyntax condition, out ExpressionSyntax? operand)
    {
        operand = null;
        switch (condition)
        {
            case IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax { Expression: { } constant } } pattern when constant.IsKind(SyntaxKind.NullLiteralExpression):
            {
                operand = pattern.Expression;
                return true;
            }

            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression):
            {
                operand = OtherNullOperand(binary);
                return operand is not null;
            }

            default:
                return false;
        }
    }

    /// <summary>Returns the non-null side of an equality whose other side is the <c>null</c> literal.</summary>
    /// <param name="binary">The equality expression.</param>
    /// <returns>The non-null operand, or <see langword="null"/> when neither side is the null literal.</returns>
    private static ExpressionSyntax? OtherNullOperand(BinaryExpressionSyntax binary)
    {
        if (binary.Right.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return binary.Left;
        }

        return binary.Left.IsKind(SyntaxKind.NullLiteralExpression) ? binary.Right : null;
    }

    /// <summary>Returns whether a constructor's paramName argument is absent or names the checked expression.</summary>
    /// <param name="argumentList">The exception constructor's argument list (may be <see langword="null"/>).</param>
    /// <param name="checkedExpression">The checked expression, used to validate the paramName.</param>
    /// <returns><see langword="true"/> when there is no argument, or a single matching <c>nameof</c>/string argument.</returns>
    private static bool ParamNameArgumentMatches(ArgumentListSyntax? argumentList, ExpressionSyntax checkedExpression)
    {
        if (argumentList is not { Arguments.Count: > 0 } list)
        {
            return true;
        }

        if (list.Arguments.Count != 1)
        {
            return false;
        }

        var expectedText = checkedExpression.ToString();
        return list.Arguments[0].Expression switch
        {
            InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } } nameofCall => NameofArgument(nameofCall) == expectedText,
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText == expectedText,
            _ => false,
        };
    }

    /// <summary>Returns the textual argument of a single-argument <c>nameof(...)</c>, or null.</summary>
    /// <param name="nameofCall">The <c>nameof</c> invocation.</param>
    /// <returns>The argument text, or <see langword="null"/> when the call does not take exactly one argument.</returns>
    private static string? NameofArgument(InvocationExpressionSyntax nameofCall)
    {
        var arguments = nameofCall.ArgumentList.Arguments;
        return arguments.Count == 1 ? arguments[0].Expression.ToString() : null;
    }

    /// <summary>Returns the right-most simple name of a (possibly qualified) type syntax.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns>The simple name, or <see langword="null"/> when it is not a name syntax.</returns>
    private static string? SimpleTypeName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
        _ => null,
    };
}
