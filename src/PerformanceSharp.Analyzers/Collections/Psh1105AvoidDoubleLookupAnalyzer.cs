// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a membership guard whose only purpose is to protect a mutating call that already
/// reports whether it acted (PSH1105). An <c>if</c> statement with no else and a single
/// invocation body qualifies for four pairings: <c>ContainsKey</c> guarding <c>Remove</c>,
/// <c>!ContainsKey</c> guarding <c>Add</c> (only when the receiver exposes an accessible
/// <c>TryAdd</c>), <c>!Contains</c> guarding a bool-returning <c>Add</c>, and
/// <c>Contains</c> guarding a bool-returning <c>Remove</c>. The guard and body must use
/// syntactically equivalent simple receivers and keys so dropping the guard is safe.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1105AvoidDoubleLookupAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The dictionary membership guard method name.</summary>
    internal const string ContainsKeyMethodName = "ContainsKey";

    /// <summary>The set membership guard method name.</summary>
    internal const string ContainsMethodName = "Contains";

    /// <summary>The insertion mutator method name.</summary>
    internal const string AddMethodName = "Add";

    /// <summary>The removal mutator method name.</summary>
    internal const string RemoveMethodName = "Remove";

    /// <summary>The guard-free insertion method probed on the receiver's type.</summary>
    internal const string TryAddMethodName = "TryAdd";

    /// <summary>The argument count of the key-and-value <c>Add</c> pairing.</summary>
    private const int AddKeyValueArgumentCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.AvoidDoubleLookup);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeIfStatement, SyntaxKind.IfStatement);
    }

    /// <summary>Extracts the guarded-mutation shape for an if statement using syntax only.</summary>
    /// <param name="ifStatement">The candidate if statement.</param>
    /// <param name="shape">The validated shape when the statement matches a supported pairing.</param>
    /// <returns><see langword="true"/> when the statement is a membership guard around one matching mutating call.</returns>
    internal static bool TryGetShape(IfStatementSyntax ifStatement, out DoubleLookupShape shape)
    {
        shape = default;
        if (ifStatement.Else is not null
            || GetSingleInvocationBody(ifStatement.Statement) is not { Expression: InvocationExpressionSyntax mutation } body
            || mutation.Expression is not MemberAccessExpressionSyntax mutationAccess
            || !mutationAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || GetGuardInvocation(ifStatement.Condition, out var negated) is not { Expression: MemberAccessExpressionSyntax guardAccess } guard
            || !guardAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression))
        {
            return false;
        }

        if (!IsSupportedPairing(
                guardAccess.Name.Identifier.ValueText,
                negated,
                mutationAccess.Name.Identifier.ValueText,
                guard.ArgumentList.Arguments.Count,
                mutation.ArgumentList.Arguments.Count,
                out var requiresTryAdd)
            || !HasMatchingOperands(guard, guardAccess, mutation, mutationAccess))
        {
            return false;
        }

        shape = new(guard, guardAccess.Name, mutation, mutationAccess.Name, body, requiresTryAdd);
        return true;
    }

    /// <summary>Reports PSH1105 when a membership guard repeats the lookup a mutating call already performs.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context)
    {
        var ifStatement = (IfStatementSyntax)context.Node;
        if (!TryGetShape(ifStatement, out var shape)
            || !IsRedundantGuard(context.SemanticModel, shape, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.AvoidDoubleLookup,
            Location.Create(shape.GuardName.SyntaxTree, shape.GuardName.Span),
            shape.GuardName.Identifier.ValueText,
            GetSuggestedMutatorName(shape)));
    }

    /// <summary>Returns the mutator name to suggest in the diagnostic message.</summary>
    /// <param name="shape">The validated shape.</param>
    /// <returns><c>TryAdd</c> for the guarded two-argument Add pairing, otherwise the body's own method name.</returns>
    private static string GetSuggestedMutatorName(in DoubleLookupShape shape)
        => shape.RequiresTryAdd ? TryAddMethodName : shape.MutationName.Identifier.ValueText;

    /// <summary>Returns the single expression statement forming an if statement's body, if any.</summary>
    /// <param name="statement">The if statement's embedded statement.</param>
    /// <returns>The body expression statement, or <see langword="null"/> when the body is not exactly one expression statement.</returns>
    private static ExpressionStatementSyntax? GetSingleInvocationBody(StatementSyntax statement)
    {
        if (statement is BlockSyntax block)
        {
            if (block.Statements.Count != 1)
            {
                return null;
            }

            statement = block.Statements[0];
        }

        return statement as ExpressionStatementSyntax;
    }

    /// <summary>Returns the guard invocation from an if condition, unwrapping one logical negation.</summary>
    /// <param name="condition">The if statement's condition.</param>
    /// <param name="negated">Whether the condition negates the guard invocation.</param>
    /// <returns>The guard invocation, or <see langword="null"/> when the condition has another shape.</returns>
    private static InvocationExpressionSyntax? GetGuardInvocation(ExpressionSyntax condition, out bool negated)
    {
        negated = false;
        if (condition is PrefixUnaryExpressionSyntax prefix && prefix.IsKind(SyntaxKind.LogicalNotExpression))
        {
            negated = true;
            condition = prefix.Operand;
        }

        return condition as InvocationExpressionSyntax;
    }

    /// <summary>Returns whether the guard and mutator names form one of the four supported pairings.</summary>
    /// <param name="guardName">The guard method name.</param>
    /// <param name="negated">Whether the guard is negated in the condition.</param>
    /// <param name="mutationName">The body's method name.</param>
    /// <param name="guardArgumentCount">The guard invocation's argument count.</param>
    /// <param name="mutationArgumentCount">The body invocation's argument count.</param>
    /// <param name="requiresTryAdd">Set when the pairing rewrites to <c>TryAdd</c> rather than dropping the guard.</param>
    /// <returns><see langword="true"/> when the pairing is supported.</returns>
    private static bool IsSupportedPairing(
        string guardName,
        bool negated,
        string mutationName,
        int guardArgumentCount,
        int mutationArgumentCount,
        out bool requiresTryAdd)
    {
        requiresTryAdd = false;
        if (guardArgumentCount != 1)
        {
            return false;
        }

        switch (guardName)
        {
            case ContainsKeyMethodName when !negated:
                return mutationName == RemoveMethodName && mutationArgumentCount == 1;

            case ContainsKeyMethodName:
            {
                requiresTryAdd = mutationName == AddMethodName && mutationArgumentCount == AddKeyValueArgumentCount;
                return requiresTryAdd;
            }

            case ContainsMethodName when negated:
                return mutationName == AddMethodName && mutationArgumentCount == 1;

            case ContainsMethodName:
                return mutationName == RemoveMethodName && mutationArgumentCount == 1;

            default:
                return false;
        }
    }

    /// <summary>Returns whether the guard and body use equivalent simple receivers and keys.</summary>
    /// <param name="guard">The guard invocation.</param>
    /// <param name="guardAccess">The guard's member access.</param>
    /// <param name="mutation">The body invocation.</param>
    /// <param name="mutationAccess">The body's member access.</param>
    /// <returns><see langword="true"/> when dropping the guard duplicates no side effects.</returns>
    private static bool HasMatchingOperands(
        InvocationExpressionSyntax guard,
        MemberAccessExpressionSyntax guardAccess,
        InvocationExpressionSyntax mutation,
        MemberAccessExpressionSyntax mutationAccess)
    {
        var guardKey = guard.ArgumentList.Arguments[0];
        var mutationArguments = mutation.ArgumentList.Arguments;
        if (!LookupGuardHelper.IsPlainArgument(guardKey)
            || !LookupGuardHelper.IsPlainArgument(mutationArguments[0])
            || (mutationArguments.Count == AddKeyValueArgumentCount && !LookupGuardHelper.IsPlainArgument(mutationArguments[1])))
        {
            return false;
        }

        return LookupGuardHelper.IsSimpleReceiver(guardAccess.Expression)
            && LookupGuardHelper.IsSimpleKey(guardKey.Expression)
            && SyntaxFactory.AreEquivalent(guardAccess.Expression, mutationAccess.Expression)
            && SyntaxFactory.AreEquivalent(guardKey.Expression, mutationArguments[0].Expression);
    }

    /// <summary>Returns whether the bound symbols confirm the guard repeats a lookup the mutator already performs.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="shape">The validated shape.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the guard is redundant for the bound methods.</returns>
    private static bool IsRedundantGuard(SemanticModel model, in DoubleLookupShape shape, CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(shape.Guard, cancellationToken).Symbol is not IMethodSymbol { ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: 1 })
        {
            return false;
        }

        if (shape.RequiresTryAdd)
        {
            var receiver = ((MemberAccessExpressionSyntax)shape.Mutation.Expression).Expression;
            return model.GetSymbolInfo(shape.Mutation, cancellationToken).Symbol is IMethodSymbol
                && model.GetTypeInfo(receiver, cancellationToken).Type is { } receiverType
                && LookupGuardHelper.TypeExposesAccessibleMethod(receiverType, TryAddMethodName, secondParameterIsOut: false, model, shape.Mutation.SpanStart);
        }

        return model.GetSymbolInfo(shape.Mutation, cancellationToken).Symbol is IMethodSymbol { ReturnType.SpecialType: SpecialType.System_Boolean };
    }

    /// <summary>Describes one validated membership guard around a single mutating call.</summary>
    /// <param name="Guard">The guard invocation in the if condition.</param>
    /// <param name="GuardName">The guard's member name (used for reporting).</param>
    /// <param name="Mutation">The mutating invocation in the if body.</param>
    /// <param name="MutationName">The mutating call's member name.</param>
    /// <param name="Body">The body expression statement that replaces the if statement.</param>
    /// <param name="RequiresTryAdd">Whether the fix rewrites the body's <c>Add</c> to <c>TryAdd</c>.</param>
    internal readonly record struct DoubleLookupShape(
        InvocationExpressionSyntax Guard,
        SimpleNameSyntax GuardName,
        InvocationExpressionSyntax Mutation,
        SimpleNameSyntax MutationName,
        ExpressionStatementSyntax Body,
        bool RequiresTryAdd);
}
