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
    /// <summary>The <c>string.IsNullOrEmpty</c> guard method name.</summary>
    public const string IsNullOrEmpty = "IsNullOrEmpty";

    /// <summary>The <c>string.IsNullOrWhiteSpace</c> guard method name.</summary>
    public const string IsNullOrWhiteSpace = "IsNullOrWhiteSpace";

    /// <summary>Matches a standard instance disposed guard.</summary>
    /// <param name="ifStatement">The candidate if statement.</param>
    /// <param name="condition">The disposed condition.</param>
    /// <returns><see langword="true"/> when the guard can use <c>ObjectDisposedException.ThrowIf</c>.</returns>
    public static bool TryMatchObjectDisposed(IfStatementSyntax ifStatement, out ExpressionSyntax? condition)
    {
        condition = null;
        if (ifStatement.Else is not null
            || !TryGetThrownCreation(ifStatement.Statement, out var creation)
            || SimpleTypeName(creation!.Type) != "ObjectDisposedException"
            || !HasStandardDisposedArguments(creation.ArgumentList))
        {
            return false;
        }

        condition = ifStatement.Condition;
        return true;
    }

    /// <summary>Matches a simple comparison guard that maps exactly to an out-of-range helper.</summary>
    /// <param name="ifStatement">The candidate if statement.</param>
    /// <param name="match">The helper and operands when matched.</param>
    /// <returns><see langword="true"/> when a replacement is available.</returns>
    public static bool TryMatchRangeGuard(IfStatementSyntax ifStatement, out RangeGuardMatch match)
    {
        match = default;
        if (!TryGetRangeGuardParts(ifStatement, out var binary, out var parameterName))
        {
            return false;
        }

        var leftMatches = IsIdentifier(binary!.Left, parameterName!);
        var rightMatches = IsIdentifier(binary.Right, parameterName!);
        if (leftMatches == rightMatches)
        {
            return false;
        }

        var value = leftMatches ? binary.Left : binary.Right;
        var bound = leftMatches ? binary.Right : binary.Left;
        var kind = leftMatches ? binary.Kind() : Reverse(binary.Kind());
        var helper = RangeHelper(kind, bound);
        if (helper is null)
        {
            return false;
        }

        match = new(helper, value, IsZero(bound) && HelperHasSingleArgument(helper) ? null : bound);
        return true;
    }

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
            || SimpleTypeName(creation!.Type) != "ArgumentNullException"
            || !TryGetNullCheckOperand(ifStatement.Condition, out var operand)
            || !ParamNameArgumentMatches(creation.ArgumentList, operand!))
        {
            return false;
        }

        checkedExpression = operand;
        return true;
    }

    /// <summary>
    /// Matches <c>if (string.IsNullOrEmpty(E)) throw new ArgumentException/ArgumentNullException(...);</c>
    /// (and the <c>IsNullOrWhiteSpace</c> form). Unlike the null guard this does not constrain the
    /// constructor arguments — <see cref="System.ArgumentException"/>'s first argument is a message,
    /// not a parameter name, so the helper substitutes its own standard message. That message change
    /// is why the corresponding rules are opt-in.
    /// </summary>
    /// <param name="ifStatement">The candidate if statement.</param>
    /// <param name="guardMethod">The matched guard method name (<see cref="IsNullOrEmpty"/> or <see cref="IsNullOrWhiteSpace"/>).</param>
    /// <param name="checkedExpression">The checked string expression when matched.</param>
    /// <returns><see langword="true"/> when the statement is a replaceable string guard.</returns>
    public static bool TryMatchStringGuard(IfStatementSyntax ifStatement, out string? guardMethod, out ExpressionSyntax? checkedExpression)
    {
        guardMethod = null;
        checkedExpression = null;
        return ifStatement.Else is null
            && TryGetThrownCreation(ifStatement.Statement, out var creation)
            && IsArgumentExceptionName(SimpleTypeName(creation!.Type))
            && TryGetStringGuard(ifStatement.Condition, out guardMethod, out checkedExpression);
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
            BlockSyntax { Statements: [ThrowStatementSyntax single] } => single,
            _ => null
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

    /// <summary>Returns the argument of a <c>string.IsNullOrEmpty</c>/<c>IsNullOrWhiteSpace</c> condition.</summary>
    /// <param name="condition">The if condition.</param>
    /// <param name="guardMethod">The matched guard method name.</param>
    /// <param name="operand">The single checked argument when matched.</param>
    /// <returns><see langword="true"/> when the condition is a recognized string guard.</returns>
    private static bool TryGetStringGuard(ExpressionSyntax condition, out string? guardMethod, out ExpressionSyntax? operand)
    {
        guardMethod = null;
        operand = null;
        if (condition is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access, ArgumentList.Arguments: [var argument] }
            || !IsStringReceiver(access.Expression))
        {
            return false;
        }

        var name = access.Name.Identifier.Text;
        if (name is not (IsNullOrEmpty or IsNullOrWhiteSpace))
        {
            return false;
        }

        guardMethod = name;
        operand = argument.Expression;
        return true;
    }

    /// <summary>Returns whether the receiver of a member access is the <c>string</c> type.</summary>
    /// <param name="receiver">The receiver expression.</param>
    /// <returns><see langword="true"/> for the <c>string</c> keyword or the <c>String</c> identifier.</returns>
    private static bool IsStringReceiver(ExpressionSyntax receiver) => receiver switch
    {
        PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.StringKeyword),
        IdentifierNameSyntax identifier => identifier.Identifier.Text is "string" or "String",
        MemberAccessExpressionSyntax qualified => qualified.Name.Identifier.Text is "String",
        _ => false
    };

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

        if (list.Arguments is not [var only])
        {
            return false;
        }

        var expectedText = checkedExpression.ToString();
        return only.Expression switch
        {
            InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" }, ArgumentList.Arguments: [var named] } => named.Expression.ToString() == expectedText,
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText == expectedText,
            _ => false
        };
    }

    /// <summary>Extracts the binary comparison and named parameter from a range guard.</summary>
    /// <param name="ifStatement">The candidate statement.</param>
    /// <param name="binary">The binary comparison.</param>
    /// <param name="parameterName">The guarded parameter name.</param>
    /// <returns><see langword="true"/> when the outer guard shape matches.</returns>
    private static bool TryGetRangeGuardParts(
        IfStatementSyntax ifStatement,
        out BinaryExpressionSyntax? binary,
        out string parameterName)
    {
        binary = ifStatement.Condition as BinaryExpressionSyntax;
        parameterName = string.Empty;
        return ifStatement.Else is null
            && binary is not null
            && TryGetThrownCreation(ifStatement.Statement, out var creation)
            && SimpleTypeName(creation!.Type) == "ArgumentOutOfRangeException"
            && TryGetParamName(creation.ArgumentList, out parameterName);
    }

    /// <summary>Returns whether disposed-exception arguments carry no custom message.</summary>
    /// <param name="arguments">The constructor arguments.</param>
    /// <returns><see langword="true"/> for zero arguments or one type-name argument.</returns>
    private static bool HasStandardDisposedArguments(ArgumentListSyntax? arguments)
        => arguments is null
            || arguments.Arguments.Count == 0
            || (arguments.Arguments.Count == 1
                && arguments.Arguments[0].Expression is InvocationExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.Text: "nameof" }
                });

    /// <summary>Reads the identifier named by the exception's first <c>nameof</c> argument.</summary>
    /// <param name="arguments">The constructor arguments.</param>
    /// <param name="name">The named parameter.</param>
    /// <returns><see langword="true"/> when a single <c>nameof(identifier)</c> argument is present.</returns>
    private static bool TryGetParamName(ArgumentListSyntax? arguments, out string name)
    {
        name = string.Empty;
        if (arguments?.Arguments.Count != 1
            || arguments.Arguments[0].Expression is not InvocationExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.Text: "nameof" },
                ArgumentList.Arguments: [{ Expression: IdentifierNameSyntax identifier }]
            })
        {
            return false;
        }

        name = identifier.Identifier.ValueText;
        return true;
    }

    /// <summary>Returns whether an expression is the named identifier.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="name">The expected identifier name.</param>
    /// <returns><see langword="true"/> when the expression is that identifier.</returns>
    private static bool IsIdentifier(ExpressionSyntax expression, string name)
        => expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == name;

    /// <summary>Reverses a comparison kind when the guarded value is on the right.</summary>
    /// <param name="kind">The original comparison kind.</param>
    /// <returns>The reversed comparison kind.</returns>
    private static SyntaxKind Reverse(SyntaxKind kind) => kind switch
    {
        SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
        SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
        SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
        SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
        _ => kind
    };

    /// <summary>Maps a comparison kind and zero bound to the corresponding helper.</summary>
    /// <param name="kind">The normalized comparison kind.</param>
    /// <param name="bound">The comparison bound.</param>
    /// <returns>The helper name, or <see langword="null"/>.</returns>
    private static string? RangeHelper(SyntaxKind kind, ExpressionSyntax bound)
        => IsZero(bound) ? ZeroRangeHelper(kind) : BoundRangeHelper(kind);

    /// <summary>Maps a comparison against zero to a single-argument helper.</summary>
    /// <param name="kind">The comparison kind.</param>
    /// <returns>The helper name, or <see langword="null"/>.</returns>
    private static string? ZeroRangeHelper(SyntaxKind kind) => kind switch
    {
        SyntaxKind.LessThanExpression => "ThrowIfNegative",
        SyntaxKind.LessThanOrEqualExpression => "ThrowIfNegativeOrZero",
        SyntaxKind.EqualsExpression => "ThrowIfZero",
        _ => null
    };

    /// <summary>Maps a comparison against a bound to a two-argument helper.</summary>
    /// <param name="kind">The comparison kind.</param>
    /// <returns>The helper name, or <see langword="null"/>.</returns>
    private static string? BoundRangeHelper(SyntaxKind kind) => kind switch
        {
            SyntaxKind.GreaterThanExpression => "ThrowIfGreaterThan",
            SyntaxKind.GreaterThanOrEqualExpression => "ThrowIfGreaterThanOrEqual",
            SyntaxKind.LessThanExpression => "ThrowIfLessThan",
            SyntaxKind.LessThanOrEqualExpression => "ThrowIfLessThanOrEqual",
            SyntaxKind.EqualsExpression => "ThrowIfEqual",
            SyntaxKind.NotEqualsExpression => "ThrowIfNotEqual",
            _ => null
        };

    /// <summary>Returns whether an expression is the numeric zero literal.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns><see langword="true"/> when it is zero.</returns>
    private static bool IsZero(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.NumericLiteralExpression)
            && literal.Token.ValueText.AsSpan().SequenceEqual("0".AsSpan());

    /// <summary>Returns whether a helper takes only the guarded value.</summary>
    /// <param name="helper">The helper name.</param>
    /// <returns><see langword="true"/> for single-argument helpers.</returns>
    private static bool HelperHasSingleArgument(string helper)
        => helper is "ThrowIfNegative" or "ThrowIfNegativeOrZero" or "ThrowIfZero";

    /// <summary>Returns whether a simple type name is <c>ArgumentException</c> or <c>ArgumentNullException</c>.</summary>
    /// <param name="name">The simple type name.</param>
    /// <returns><see langword="true"/> for either argument-exception type.</returns>
    private static bool IsArgumentExceptionName(string? name) => name is "ArgumentException" or "ArgumentNullException";

    /// <summary>Returns the right-most simple name of a (possibly qualified) type syntax.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns>The simple name, or <see langword="null"/> when it is not a name syntax.</returns>
    private static string? SimpleTypeName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
        _ => null
    };

    /// <summary>A matched range helper and its ordered operands.</summary>
    /// <param name="Helper">The helper method name.</param>
    /// <param name="Value">The guarded value.</param>
    /// <param name="Bound">The optional bound.</param>
    internal readonly record struct RangeGuardMatch(string Helper, ExpressionSyntax Value, ExpressionSyntax? Bound);
}
