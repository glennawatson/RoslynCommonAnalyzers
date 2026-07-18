// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>System.Diagnostics.Debug.Assert</c> whose condition — its first argument — performs a side
/// effect (SST2450). <c>Debug.Assert</c> carries <c>[Conditional("DEBUG")]</c>, so a release build omits the
/// entire call and never evaluates the condition; any state written there happens in debug builds and vanishes
/// in release, so the program behaves differently between configurations.
/// </summary>
/// <remarks>
/// <para>
/// The clean path is syntax only. The rule sees every invocation, rejects all but calls whose name is
/// <c>Assert</c>, then scans the first argument's expression subtree for a side effect. Nothing binds until a
/// side effect is found in an <c>Assert</c>-named call; only then is the invocation bound to confirm it really
/// is <c>System.Diagnostics.Debug.Assert</c> (any overload — the condition is always the first parameter), so a
/// <c>Trace.Assert</c> or a same-named method of the project's own is left alone.
/// </para>
/// <para>
/// To keep false positives near zero the scan reports only three shapes it can trust from syntax alone: an
/// assignment (<c>=</c>, <c>+=</c>, …), a pre/post increment or decrement (<c>++</c>/<c>--</c>), and a call to a
/// well-known state-changing collection or enumerator method invoked on a receiver — <c>Add</c>, <c>Remove</c>,
/// the <c>Try*</c> mutators, <c>Pop</c>, <c>Dequeue</c>, <c>MoveNext</c>. Everything else in a condition stays
/// silent: comparisons, boolean operators, literals, <c>nameof</c>, parenthesization, member/property/field and
/// local reads, <c>is</c>/pattern checks, and ordinary predicate or query calls (<c>IsValid()</c>,
/// <c>Contains()</c>, <c>TryGetValue(out …)</c>) that are the normal, correct content of an assert. A curated
/// name set is used instead of binding each call because a bound symbol carries no purity signal; the trade is
/// that the pure <c>Add</c>/<c>Remove</c> of an immutable collection is not distinguished by name and a
/// conditional-access mutation (<c>list?.Remove(x)</c>) is not reached.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2450DebugAssertSideEffectAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The method name every reported call is spelled with.</summary>
    private const string AssertMethodName = "Assert";

    /// <summary>The metadata name of the type the reported call must bind to.</summary>
    private const string DebugTypeMetadataName = "System.Diagnostics.Debug";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.DebugAssertConditionSideEffect);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Resolves the Debug type once, then analyzes each invocation.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var debugType = context.Compilation.GetTypeByMetadataName(DebugTypeMetadataName);
        if (debugType is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, debugType), SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports one Debug.Assert whose condition has a side effect.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="debugType">The resolved <c>System.Diagnostics.Debug</c> type.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol debugType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsAssertNamed(invocation.Expression))
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return;
        }

        var sideEffect = FindSideEffect(arguments[0].Expression);
        if (sideEffect is null)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.Name != AssertMethodName
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, debugType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.DebugAssertConditionSideEffect,
            sideEffect.GetLocation(),
            Describe(sideEffect)));
    }

    /// <summary>Returns whether an invoked expression names <c>Assert</c>.</summary>
    /// <param name="expression">The invocation's expression.</param>
    /// <returns><see langword="true"/> for <c>Debug.Assert</c> or an <c>Assert</c> reached through <c>using static</c>.</returns>
    private static bool IsAssertNamed(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText == AssertMethodName,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == AssertMethodName,
        _ => false,
    };

    /// <summary>Finds the first side-effecting node in a condition's expression tree.</summary>
    /// <param name="condition">The condition argument's expression.</param>
    /// <returns>The offending node, or <see langword="null"/> when the condition is side-effect-free.</returns>
    private static SyntaxNode? FindSideEffect(ExpressionSyntax condition)
    {
        if (IsSideEffect(condition))
        {
            return condition;
        }

        SideEffectScan scan = default;
        DescendantTraversalHelper.VisitDescendants<SyntaxNode, SideEffectScan>(condition, ref scan, VisitNode);
        return scan.Found;
    }

    /// <summary>Records the first side-effecting descendant and stops the walk.</summary>
    /// <param name="node">The node being visited.</param>
    /// <param name="scan">The scan state.</param>
    /// <returns><see langword="false"/> once a side effect is found, which stops the walk.</returns>
    private static bool VisitNode(SyntaxNode node, ref SideEffectScan scan)
    {
        if (!IsSideEffect(node))
        {
            return true;
        }

        scan.Found = node;
        return false;
    }

    /// <summary>Returns whether a node evaluates a side effect the rule can trust from syntax alone.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> for an assignment, an increment/decrement, or a state-changing call.</returns>
    private static bool IsSideEffect(SyntaxNode node) => node switch
    {
        AssignmentExpressionSyntax => true,
        PostfixUnaryExpressionSyntax postfix => IsIncrementOrDecrement(postfix.Kind()),
        PrefixUnaryExpressionSyntax prefix => IsIncrementOrDecrement(prefix.Kind()),
        InvocationExpressionSyntax invocation => IsStateChangingCall(invocation.Expression),
        _ => false,
    };

    /// <summary>Returns whether a unary kind mutates its operand.</summary>
    /// <param name="kind">The unary expression's syntax kind.</param>
    /// <returns><see langword="true"/> for <c>++</c> and <c>--</c> in either position.</returns>
    private static bool IsIncrementOrDecrement(SyntaxKind kind)
        => kind is SyntaxKind.PostIncrementExpression
            or SyntaxKind.PostDecrementExpression
            or SyntaxKind.PreIncrementExpression
            or SyntaxKind.PreDecrementExpression;

    /// <summary>Returns whether an invoked expression is a state-changing collection or enumerator call.</summary>
    /// <param name="expression">The invocation's expression.</param>
    /// <returns><see langword="true"/> when a receiver method with a known mutating name is called.</returns>
    private static bool IsStateChangingCall(ExpressionSyntax expression)
        => expression is MemberAccessExpressionSyntax member && IsStateChangingMethodName(member.Name.Identifier.ValueText);

    /// <summary>Returns whether a method name is a well-known state-changing operation.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> for a curated set of mutating collection and enumerator methods.</returns>
    private static bool IsStateChangingMethodName(string name)
        => IsMutatingMemberName(name) || IsTryMutatingMemberName(name);

    /// <summary>Returns whether a method name is a plain mutating collection or enumerator operation.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> for <c>Add</c>, <c>Remove</c>, <c>Pop</c>, <c>Dequeue</c>, or <c>MoveNext</c>.</returns>
    private static bool IsMutatingMemberName(string name)
        => name is "Add" or "Remove" or "Pop" or "Dequeue" or "MoveNext";

    /// <summary>Returns whether a method name is one of the <c>Try*</c> mutating operations.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> for the concurrent and try-mutate members.</returns>
    private static bool IsTryMutatingMemberName(string name)
        => name is "TryAdd" or "TryRemove" or "TryTake" or "TryPop" or "TryPush" or "TryDequeue" or "TryUpdate";

    /// <summary>Describes a side-effecting node for the diagnostic message.</summary>
    /// <param name="node">The offending node.</param>
    /// <returns>The message fragment naming the kind of side effect.</returns>
    private static string Describe(SyntaxNode node) => node switch
    {
        AssignmentExpressionSyntax => "an assignment",
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } => $"a call to '{member.Name.Identifier.ValueText}'",
        _ => "an increment or decrement",
    };

    /// <summary>The state threaded through a condition's side-effect scan.</summary>
    private record struct SideEffectScan
    {
        /// <summary>Gets or sets the first side-effecting node found.</summary>
        public SyntaxNode? Found { get; set; }
    }
}
