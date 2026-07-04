// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags insert-if-absent shapes that probe a dictionary twice (PSH1115). A
/// <c>ContainsKey</c> guard around an indexer write hashes the key once to test and once to
/// store, where <c>TryAdd</c> does both in one probe; a failed <c>TryGetValue</c> followed by
/// a store repeats the lookup that <c>CollectionsMarshal.GetValueRefOrAddDefault</c> exposes
/// as a single-probe value slot. Each shape is gated on its replacement API existing in the
/// compilation, and the guard and store must name the same receiver and key.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1115SingleProbeInsertAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The guard member name of the TryAdd shape.</summary>
    internal const string ContainsKeyMethodName = "ContainsKey";

    /// <summary>The guard member name of the value-slot shape.</summary>
    internal const string TryGetValueMethodName = "TryGetValue";

    /// <summary>The replacement member name of the TryAdd shape.</summary>
    internal const string TryAddMethodName = "TryAdd";

    /// <summary>The replacement spelling of the value-slot shape.</summary>
    internal const string GetValueRefSpelling = "CollectionsMarshal.GetValueRefOrAddDefault";

    /// <summary>The store member name accepted alongside the indexer in the value-slot shape.</summary>
    internal const string AddMethodName = "Add";

    /// <summary>The metadata name of the dictionary type.</summary>
    private const string DictionaryMetadataName = "System.Collections.Generic.Dictionary`2";

    /// <summary>The metadata name of the marshal type providing the value-slot API.</summary>
    private const string CollectionsMarshalMetadataName = "System.Runtime.InteropServices.CollectionsMarshal";

    /// <summary>The member name of the value-slot API.</summary>
    private const string GetValueRefMethodName = "GetValueRefOrAddDefault";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.SingleProbeInsert);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var slotShapeEnabled = start.Compilation.GetTypeByMetadataName(CollectionsMarshalMetadataName)
                ?.GetMembers(GetValueRefMethodName).IsEmpty == false;
            var dictionaryType = start.Compilation.GetTypeByMetadataName(DictionaryMetadataName);
            if (dictionaryType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeIf(nodeContext, dictionaryType, slotShapeEnabled),
                SyntaxKind.IfStatement);
        });
    }

    /// <summary>Returns the parts of a <c>!receiver.Method(key)</c> guard condition, before any binding.</summary>
    /// <param name="ifStatement">The if statement to inspect.</param>
    /// <param name="methodName">The required guard member name.</param>
    /// <param name="argumentCount">The required guard argument count.</param>
    /// <returns>The receiver and key, or <see langword="null"/> when the shape does not match.</returns>
    internal static (ExpressionSyntax Receiver, ExpressionSyntax Key)? TryGetNegatedGuard(
        IfStatementSyntax ifStatement,
        string methodName,
        int argumentCount)
    {
        if (ifStatement.Else is not null
            || ifStatement.Condition is not PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negation
            || negation.Operand is not InvocationExpressionSyntax guard
            || guard.ArgumentList.Arguments.Count != argumentCount
            || guard.Expression is not MemberAccessExpressionSyntax access
            || access.Name.Identifier.ValueText != methodName)
        {
            return null;
        }

        return (access.Expression, guard.ArgumentList.Arguments[0].Expression);
    }

    /// <summary>Returns the indexer store assigned inside the TryAdd shape's guarded statement.</summary>
    /// <param name="ifStatement">The if statement to inspect.</param>
    /// <param name="receiver">The guard receiver the store must repeat.</param>
    /// <param name="key">The guard key the store must repeat.</param>
    /// <returns>The stored value expression, or <see langword="null"/> when the shape does not match.</returns>
    internal static ExpressionSyntax? TryGetGuardedIndexerStore(
        IfStatementSyntax ifStatement,
        ExpressionSyntax receiver,
        ExpressionSyntax key)
    {
        var statement = ifStatement.Statement is BlockSyntax { Statements: [var single] } ? single : ifStatement.Statement;
        return statement is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment }
            && IsSameElementAccess(assignment.Left, receiver, key)
            ? assignment.Right
            : null;
    }

    /// <summary>Returns whether an expression is an element access on the guard's receiver and key.</summary>
    /// <param name="expression">The store target.</param>
    /// <param name="receiver">The guard receiver.</param>
    /// <param name="key">The guard key.</param>
    /// <returns><see langword="true"/> when receiver and key are structurally identical.</returns>
    private static bool IsSameElementAccess(ExpressionSyntax expression, ExpressionSyntax receiver, ExpressionSyntax key)
        => expression is ElementAccessExpressionSyntax { ArgumentList.Arguments: [var index] } element
            && SyntaxFactory.AreEquivalent(element.Expression, receiver)
            && SyntaxFactory.AreEquivalent(index.Expression, key);

    /// <summary>Reports PSH1115 for a double-probing insert-if-absent shape.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="dictionaryType">The dictionary type definition.</param>
    /// <param name="slotShapeEnabled">Whether the value-slot API exists in the compilation.</param>
    private static void AnalyzeIf(SyntaxNodeAnalysisContext context, INamedTypeSymbol dictionaryType, bool slotShapeEnabled)
    {
        var ifStatement = (IfStatementSyntax)context.Node;
        if (TryGetTryAddShape(context, ifStatement) || !slotShapeEnabled)
        {
            return;
        }

        AnalyzeValueSlotShape(context, ifStatement, dictionaryType);
    }

    /// <summary>Reports the TryAdd shape when it matches and the receiver offers TryAdd.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="ifStatement">The if statement.</param>
    /// <returns><see langword="true"/> when the shape matched and was handled.</returns>
    private static bool TryGetTryAddShape(SyntaxNodeAnalysisContext context, IfStatementSyntax ifStatement)
    {
        if (TryGetNegatedGuard(ifStatement, ContainsKeyMethodName, argumentCount: 1) is not { } guard
            || TryGetGuardedIndexerStore(ifStatement, guard.Receiver, guard.Key) is null)
        {
            return false;
        }

        if (context.SemanticModel.GetTypeInfo(guard.Receiver, context.CancellationToken).Type is not INamedTypeSymbol receiverType
            || receiverType.OriginalDefinition.GetMembers(TryAddMethodName).IsEmpty)
        {
            return false;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.SingleProbeInsert,
            ifStatement.SyntaxTree,
            ifStatement.Span,
            TryAddMethodName));
        return true;
    }

    /// <summary>Reports the value-slot shape for a failed TryGetValue followed by a store.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="ifStatement">The if statement.</param>
    /// <param name="dictionaryType">The dictionary type definition.</param>
    private static void AnalyzeValueSlotShape(SyntaxNodeAnalysisContext context, IfStatementSyntax ifStatement, INamedTypeSymbol dictionaryType)
    {
        if (TryGetNegatedGuard(ifStatement, TryGetValueMethodName, argumentCount: 2) is not { } guard
            || !EndsWithStore(ifStatement, guard.Receiver, guard.Key))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(guard.Receiver, context.CancellationToken).Type is not INamedTypeSymbol receiverType
            || !SymbolEqualityComparer.Default.Equals(receiverType.OriginalDefinition, dictionaryType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.SingleProbeInsert,
            ifStatement.SyntaxTree,
            ifStatement.Span,
            GetValueRefSpelling));
    }

    /// <summary>Returns whether the guarded statement's last action stores into the same receiver and key.</summary>
    /// <param name="ifStatement">The if statement.</param>
    /// <param name="receiver">The guard receiver.</param>
    /// <param name="key">The guard key.</param>
    /// <returns><see langword="true"/> for a final indexer write or two-argument Add call.</returns>
    private static bool EndsWithStore(IfStatementSyntax ifStatement, ExpressionSyntax receiver, ExpressionSyntax key)
    {
        var statement = ifStatement.Statement is BlockSyntax { Statements: { Count: > 0 } statements }
            ? statements[statements.Count - 1]
            : ifStatement.Statement;
        if (statement is not ExpressionStatementSyntax { Expression: var expression })
        {
            return false;
        }

        if (expression is AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment)
        {
            return IsSameElementAccess(assignment.Left, receiver, key);
        }

        return expression is InvocationExpressionSyntax { ArgumentList.Arguments: [var first, _] } invocation
            && invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: AddMethodName } access
            && SyntaxFactory.AreEquivalent(access.Expression, receiver)
            && SyntaxFactory.AreEquivalent(first.Expression, key);
    }
}
