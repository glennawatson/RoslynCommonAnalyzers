// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests combining a <c>ContainsKey</c> guard and the indexer reads it protects into a
/// single <c>TryGetValue</c> call (PSH1104). A guard qualifies when it is the whole
/// <c>if</c> condition (or the leftmost operand of the condition's top-level <c>&amp;&amp;</c>
/// chain) or a ternary condition, the receiver and key are simple expressions that can be
/// duplicated safely, every equivalent indexer use in the guarded region is a read (with at
/// least one), and the receiver's static type exposes an accessible bool-returning
/// <c>TryGetValue(key, out value)</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1104UseTryGetValueAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The guard method name whose presence gates all further analysis.</summary>
    internal const string ContainsKeyMethodName = "ContainsKey";

    /// <summary>The replacement method name probed on the receiver's type.</summary>
    internal const string TryGetValueMethodName = "TryGetValue";

    /// <summary>The numeric C# 7 language-version value, the first with the 'out var' declaration the fix emits.</summary>
    private const int CSharp7 = 7;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.UseTryGetValue);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Extracts the guard shape for a candidate ContainsKey invocation using syntax only.</summary>
    /// <param name="invocation">The candidate ContainsKey invocation.</param>
    /// <param name="shape">The validated guard shape when the invocation qualifies.</param>
    /// <returns><see langword="true"/> when the invocation guards an if statement or ternary with simple operands.</returns>
    internal static bool TryGetGuardShape(InvocationExpressionSyntax invocation, out GuardShape shape)
    {
        shape = default;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || memberAccess.Name.Identifier.ValueText != ContainsKeyMethodName
            || invocation.ArgumentList.Arguments is not [var keyArgument]
            || !LookupGuardHelper.IsPlainArgument(keyArgument)
            || !LookupGuardHelper.IsSimpleReceiver(memberAccess.Expression)
            || !LookupGuardHelper.IsSimpleKey(keyArgument.Expression))
        {
            return false;
        }

        return TryGetGuardRegions(invocation, memberAccess.Expression, keyArgument.Expression, out shape);
    }

    /// <summary>Returns whether the guarded regions contain at least one matching indexer read and no matching write.</summary>
    /// <param name="shape">The validated guard shape.</param>
    /// <returns><see langword="true"/> when every equivalent indexer use is a read and at least one exists.</returns>
    internal static bool HasOnlyGuardedReads(in GuardShape shape)
    {
        var state = new ElementAccessScanState(shape.Receiver, shape.Key);
        ScanRegion(shape.FirstRegion, ref state);
        if (!state.HasWrite && shape.SecondRegion is { } secondRegion)
        {
            ScanRegion(secondRegion, ref state);
        }

        return state.HasRead && !state.HasWrite;
    }

    /// <summary>Returns whether an element access targets the guard's receiver with the guard's key.</summary>
    /// <param name="elementAccess">The element access to test.</param>
    /// <param name="receiver">The guard's receiver expression.</param>
    /// <param name="key">The guard's key expression.</param>
    /// <returns><see langword="true"/> when the receiver and key are syntactically equivalent to the guard's.</returns>
    internal static bool IsMatchingElementAccess(ElementAccessExpressionSyntax elementAccess, ExpressionSyntax receiver, ExpressionSyntax key)
        => elementAccess.ArgumentList.Arguments is [var argument]
            && SyntaxFactory.AreEquivalent(elementAccess.Expression, receiver)
            && SyntaxFactory.AreEquivalent(argument.Expression, key);

    /// <summary>Reports PSH1104 when a ContainsKey guard only protects indexer reads and TryGetValue is available.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: ContainsKeyMethodName } memberAccess
            || invocation.SyntaxTree.Options is not CSharpParseOptions options
            || (int)options.LanguageVersion < CSharp7)
        {
            return;
        }

        if (!TryGetGuardShape(invocation, out var shape)
            || !HasOnlyGuardedReads(shape)
            || !HasAccessibleTryGetValue(context.SemanticModel, invocation, shape.Receiver, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseTryGetValue,
            memberAccess.Name.SyntaxTree,
            memberAccess.Name.Span));
    }

    /// <summary>Resolves the guarded regions for a syntactically validated ContainsKey invocation.</summary>
    /// <param name="invocation">The validated ContainsKey invocation.</param>
    /// <param name="receiver">The guard's receiver expression.</param>
    /// <param name="key">The guard's key expression.</param>
    /// <param name="shape">The guard shape when the invocation sits in a supported guard position.</param>
    /// <returns><see langword="true"/> when the invocation guards an if statement or a ternary true branch.</returns>
    private static bool TryGetGuardRegions(InvocationExpressionSyntax invocation, ExpressionSyntax receiver, ExpressionSyntax key, out GuardShape shape)
    {
        ExpressionSyntax current = invocation;
        while (current.Parent is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.LogicalAndExpression) && binary.Left == current)
        {
            current = binary;
        }

        if (current.Parent is IfStatementSyntax ifStatement && ifStatement.Condition == current)
        {
            shape = new(receiver, key, ifStatement.Condition, ifStatement.Statement);
            return true;
        }

        if (current == invocation && current.Parent is ConditionalExpressionSyntax conditional && conditional.Condition == current)
        {
            shape = new(receiver, key, conditional.WhenTrue, null);
            return true;
        }

        shape = default;
        return false;
    }

    /// <summary>Returns whether ContainsKey binds to a one-parameter method and the receiver's type offers TryGetValue.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The ContainsKey invocation.</param>
    /// <param name="receiver">The guard's receiver expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the rewrite target exists and is accessible.</returns>
    private static bool HasAccessibleTryGetValue(SemanticModel model, InvocationExpressionSyntax invocation, ExpressionSyntax receiver, CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { Parameters.Length: 1 }
            && model.GetTypeInfo(receiver, cancellationToken).Type is { } receiverType
            && LookupGuardHelper.TypeExposesAccessibleMethod(receiverType, TryGetValueMethodName, secondParameterIsOut: true, model, invocation.SpanStart);

    /// <summary>Scans one guarded region (including its root node) for matching indexer uses.</summary>
    /// <param name="region">The guarded region root.</param>
    /// <param name="state">The scan state accumulating read and write facts.</param>
    private static void ScanRegion(SyntaxNode region, ref ElementAccessScanState state)
    {
        if (region is ElementAccessExpressionSyntax elementAccess && !VisitElementAccess(elementAccess, ref state))
        {
            return;
        }

        DescendantTraversalHelper.VisitDescendants<ElementAccessExpressionSyntax, ElementAccessScanState>(region, ref state, VisitElementAccess);
    }

    /// <summary>Classifies one element access encountered during a guarded-region scan.</summary>
    /// <param name="elementAccess">The visited element access.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once a write disqualifies the guard.</returns>
    private static bool VisitElementAccess(ElementAccessExpressionSyntax elementAccess, ref ElementAccessScanState state)
    {
        if (!IsMatchingElementAccess(elementAccess, state.Receiver, state.Key))
        {
            return true;
        }

        if (IsWriteTarget(elementAccess))
        {
            state.HasWrite = true;
            return false;
        }

        state.HasRead = true;
        return true;
    }

    /// <summary>Returns whether an element access is used as a write target rather than a read.</summary>
    /// <param name="elementAccess">The element access to classify.</param>
    /// <returns><see langword="true"/> when the element is assigned, incremented, decremented, or passed by reference.</returns>
    private static bool IsWriteTarget(ElementAccessExpressionSyntax elementAccess)
        => elementAccess.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == elementAccess,
            PrefixUnaryExpressionSyntax prefix =>
                prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression),
            PostfixUnaryExpressionSyntax postfix =>
                postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression),
            RefExpressionSyntax => true,
            ArgumentSyntax argument => IsWriteArgument(argument),
            _ => false
        };

    /// <summary>Returns whether an argument position writes to its element-access expression.</summary>
    /// <param name="argument">The argument wrapping the element access.</param>
    /// <returns><see langword="true"/> for <c>ref</c>/<c>out</c> arguments and deconstruction targets.</returns>
    private static bool IsWriteArgument(ArgumentSyntax argument)
        => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
            || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)
            || IsDeconstructionTarget(argument);

    /// <summary>Returns whether an argument is a component of a deconstruction assignment target.</summary>
    /// <param name="argument">The argument wrapping the element access.</param>
    /// <returns><see langword="true"/> when the enclosing tuple expression is the left side of an assignment.</returns>
    private static bool IsDeconstructionTarget(ArgumentSyntax argument)
    {
        if (argument.Parent is not TupleExpressionSyntax tuple)
        {
            return false;
        }

        ExpressionSyntax current = tuple;
        while (current.Parent is ArgumentSyntax { Parent: TupleExpressionSyntax outerTuple })
        {
            current = outerTuple;
        }

        return current.Parent is AssignmentExpressionSyntax assignment && assignment.Left == current;
    }

    /// <summary>Describes one validated ContainsKey guard and the regions it protects.</summary>
    /// <param name="Receiver">The dictionary receiver expression.</param>
    /// <param name="Key">The key expression passed to ContainsKey.</param>
    /// <param name="FirstRegion">The first guarded region: the full if condition, or the ternary's true branch.</param>
    /// <param name="SecondRegion">The if statement's guarded statement, or <see langword="null"/> for the ternary shape.</param>
    internal readonly record struct GuardShape(
        ExpressionSyntax Receiver,
        ExpressionSyntax Key,
        SyntaxNode FirstRegion,
        SyntaxNode? SecondRegion);

    /// <summary>Tracks matching indexer uses while scanning the guarded regions.</summary>
    /// <param name="Receiver">The guard's receiver expression.</param>
    /// <param name="Key">The guard's key expression.</param>
    private record struct ElementAccessScanState(ExpressionSyntax Receiver, ExpressionSyntax Key)
    {
        /// <summary>Gets or sets a value indicating whether a matching indexer read was found.</summary>
        public bool HasRead { get; set; }

        /// <summary>Gets or sets a value indicating whether a matching indexer write was found.</summary>
        public bool HasWrite { get; set; }
    }
}
