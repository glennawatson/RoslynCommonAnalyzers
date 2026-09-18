// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>Checks local reads for constant-sensitive control flow and numeric conversions.</summary>
internal static class ConstLocalUsageSafety
{
    /// <summary>Checks a never-written local after its initializer has been proved constant.</summary>
    /// <param name="scope">The scope containing every read.</param>
    /// <param name="name">The local's name.</param>
    /// <param name="model">The semantic model for the scope.</param>
    /// <param name="cancellationToken">The analysis cancellation token.</param>
    /// <returns>Whether none of the guarded constant-sensitive uses requires the local to remain mutable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsSafe(SyntaxNode scope, string name, SemanticModel model, CancellationToken cancellationToken) =>
        ReadState.Scan(scope, name, model, cancellationToken);

    /// <summary>Carries one candidate's read checks without allocating a collection or per-read state.</summary>
    /// <param name="name">The candidate local's name.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The analysis cancellation token.</param>
    private readonly struct ReadState(string name, SemanticModel model, CancellationToken cancellationToken)
    {
        /// <summary>The maximum nesting inspected before treating constant folding as possible.</summary>
        private const int MaximumExpressionDepth = 32;

        /// <summary>Stops at the first read whose constant interpretation can change behavior.</summary>
        /// <param name="scope">The local's scope.</param>
        /// <param name="name">The candidate local's name.</param>
        /// <param name="model">The semantic model.</param>
        /// <param name="cancellationToken">The analysis cancellation token.</param>
        /// <returns>Whether every read can retain its interpretation.</returns>
        internal static bool Scan(SyntaxNode scope, string name, SemanticModel model, CancellationToken cancellationToken)
        {
            var state = new ReadState(name, model, cancellationToken);
            return DescendantTraversalHelper.VisitDescendants(
                scope,
                ref state,
                static (IdentifierNameSyntax identifier, ref ReadState scan) => scan.IsSafe(identifier));
        }

        /// <summary>Returns whether the read supplies a name rather than a runtime value.</summary>
        /// <param name="expression">The expression being inspected.</param>
        /// <param name="model">The semantic model.</param>
        /// <param name="cancellationToken">The analysis cancellation token.</param>
        /// <returns>Whether it is the operand of the <c>nameof</c> operator.</returns>
        private static bool IsNameOfOperand(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken) =>
            expression.Parent is ArgumentSyntax
            {
                Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } invocation },
            }
            && model.GetConstantValue(invocation, cancellationToken).HasValue;

        /// <summary>Returns whether an expression controls branch or loop reachability.</summary>
        /// <param name="expression">The expression being inspected.</param>
        /// <returns>Whether constant folding can change reachability diagnostics.</returns>
        private static bool IsControlCondition(ExpressionSyntax expression) => expression.Parent switch
        {
            WhileStatementSyntax statement => statement.Condition == expression,
            DoStatementSyntax statement => statement.Condition == expression,
            ForStatementSyntax statement => statement.Condition == expression,
            IfStatementSyntax statement => statement.Condition == expression,
            ConditionalExpressionSyntax conditional => conditional.Condition == expression,
            _ => false,
        };

        /// <summary>Returns whether the expression can become constant when eligible locals are fixed together.</summary>
        /// <param name="expression">The expression containing a candidate read.</param>
        /// <param name="name">The candidate local's name.</param>
        /// <param name="model">The semantic model.</param>
        /// <param name="cancellationToken">The analysis cancellation token.</param>
        /// <param name="depth">The current expression nesting depth.</param>
        /// <returns>Whether constant folding is possible or the nesting limit is reached.</returns>
        private static bool CanBecomeConstant(ExpressionSyntax expression, string name, SemanticModel model, CancellationToken cancellationToken, int depth = 0) =>
            depth >= MaximumExpressionDepth || CanBecomeConstantCore(expression, name, model, depth + 1, cancellationToken);

        /// <summary>Inspects one bounded expression level for runtime inputs.</summary>
        /// <param name="expression">The expression containing a candidate read.</param>
        /// <param name="name">The candidate local's name.</param>
        /// <param name="model">The semantic model.</param>
        /// <param name="depth">The depth to pass to child expressions.</param>
        /// <param name="cancellationToken">The analysis cancellation token.</param>
        /// <returns>Whether constant folding remains possible.</returns>
        private static bool CanBecomeConstantCore(ExpressionSyntax expression, string name, SemanticModel model, int depth, CancellationToken cancellationToken) => expression switch
        {
            LiteralExpressionSyntax => true,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == name || IsConstantLocal(identifier, model, cancellationToken),
            ParenthesizedExpressionSyntax parenthesized => CanBecomeConstant(parenthesized.Expression, name, model, cancellationToken, depth),
            CastExpressionSyntax cast => CanBecomeConstant(cast.Expression, name, model, cancellationToken, depth),
            CheckedExpressionSyntax checkedExpression => CanBecomeConstant(checkedExpression.Expression, name, model, cancellationToken, depth),
            PrefixUnaryExpressionSyntax prefix => CanBecomeConstant(prefix.Operand, name, model, cancellationToken, depth),
            IsPatternExpressionSyntax pattern => CanBecomeConstant(pattern.Expression, name, model, cancellationToken, depth),
            BinaryExpressionSyntax binary => CanBecomeConstant(binary.Left, name, model, cancellationToken, depth) && CanBecomeConstant(binary.Right, name, model, cancellationToken, depth),
            ConditionalExpressionSyntax conditional => CanBecomeConstant(conditional.Condition, name, model, cancellationToken, depth)
                && CanBecomeConstant(conditional.WhenTrue, name, model, cancellationToken, depth)
                && CanBecomeConstant(conditional.WhenFalse, name, model, cancellationToken, depth),
            _ => model.GetConstantValue(expression, cancellationToken).HasValue,
        };

        /// <summary>Recognizes constants and other locals that Fix All could make constant.</summary>
        /// <param name="identifier">The other value referenced by a condition.</param>
        /// <param name="model">The semantic model.</param>
        /// <param name="cancellationToken">The analysis cancellation token.</param>
        /// <returns>Whether this value can become constant without changing its stored value.</returns>
        private static bool IsConstantLocal(IdentifierNameSyntax identifier, SemanticModel model, CancellationToken cancellationToken)
        {
            if (model.GetConstantValue(identifier, cancellationToken).HasValue)
            {
                return true;
            }

            if (model.GetSymbolInfo(identifier, cancellationToken).Symbol is not ILocalSymbol { DeclaringSyntaxReferences.Length: 1 } local
                || local.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) is not VariableDeclaratorSyntax
                {
                    Initializer: { } initializer,
                    Parent: VariableDeclarationSyntax { Variables.Count: 1, Parent: LocalDeclarationStatementSyntax declaration },
                }
                || declaration.Declaration.Type is RefTypeSyntax
                || !model.GetConstantValue(initializer.Value, cancellationToken).HasValue
                || Psh1402PreferConstOverStaticReadonlyAnalyzer.GetEnclosingScope(declaration) is not { } scope)
            {
                return false;
            }

            return !Psh1402PreferConstOverStaticReadonlyAnalyzer.IsWrittenInScope(scope, identifier.Identifier.ValueText);
        }

        /// <summary>Recognizes numeric types whose constant arithmetic is checked by the compiler.</summary>
        /// <param name="type">The arithmetic result type.</param>
        /// <returns>Whether folding can introduce an overflow diagnostic.</returns>
        private static bool IsCheckedArithmeticType(ITypeSymbol? type) =>
            type?.SpecialType is SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal;

        /// <summary>Checks the local name without treating a member-access name as a local.</summary>
        /// <param name="identifier">The visited identifier.</param>
        /// <param name="name">The candidate local's name.</param>
        /// <returns>Whether the identifier can read the candidate local.</returns>
        private static bool IsLocalName(IdentifierNameSyntax identifier, string name) =>
            identifier.Identifier.ValueText == name
                && !(identifier.Parent is MemberAccessExpressionSyntax { Name: var memberName } && memberName == identifier);

        /// <summary>Checks matching identifiers and every expression that can change their interpretation.</summary>
        /// <param name="identifier">The identifier reached by the traversal.</param>
        /// <returns>Whether analysis can continue without rejecting the candidate.</returns>
        private bool IsSafe(IdentifierNameSyntax identifier)
        {
            if (!IsLocalName(identifier, name))
            {
                return true;
            }

            for (ExpressionSyntax? expression = identifier; expression is not null; expression = expression.Parent as ExpressionSyntax)
            {
                if (IsNameOfOperand(expression, model, cancellationToken))
                {
                    return true;
                }

                if (IsControlCondition(expression) && CanBecomeConstant(expression, name, model, cancellationToken))
                {
                    return false;
                }
            }

            for (ExpressionSyntax? expression = identifier; expression is not null; expression = expression.Parent as ExpressionSyntax)
            {
                if (HasConstantSensitiveConversion(expression) || IsDynamicArgument(expression) || HasInvalidConstantArithmetic(expression))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Returns whether a constant could gain a narrowing conversion and select a different overload.</summary>
        /// <param name="expression">The expression containing the read.</param>
        /// <returns>Whether its nonidentity integer conversion needs the variable's runtime type.</returns>
        private bool HasConstantSensitiveConversion(ExpressionSyntax expression) =>
            model.GetTypeInfo(expression, cancellationToken).Type?.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64
                && !model.GetConversion(expression, cancellationToken).IsIdentity
                && CanBecomeConstant(expression, name, model, cancellationToken);

        /// <summary>Returns whether runtime overload resolution would receive a constant argument.</summary>
        /// <param name="expression">The expression containing the read.</param>
        /// <returns>Whether a dynamic call consumes the expression.</returns>
        private bool IsDynamicArgument(ExpressionSyntax expression) =>
            expression.Parent is ArgumentSyntax { Parent: BaseArgumentListSyntax { Parent: ExpressionSyntax call } }
                && model.GetTypeInfo(call, cancellationToken).Type?.TypeKind == TypeKind.Dynamic;

        /// <summary>Checks arithmetic that becomes a compile-time expression after fixing eligible locals.</summary>
        /// <param name="expression">The expression containing the read.</param>
        /// <returns>Whether constant folding can introduce an invalid arithmetic operation.</returns>
        private bool HasInvalidConstantArithmetic(ExpressionSyntax expression) =>
            expression is BinaryExpressionSyntax or PrefixUnaryExpressionSyntax or CastExpressionSyntax
                && IsCheckedArithmeticType(model.GetTypeInfo(expression, cancellationToken).Type)
                && CanBecomeConstant(expression, name, model, cancellationToken)
                && !ConstInt32Arithmetic.IsSafe(expression, model, cancellationToken);
    }
}
