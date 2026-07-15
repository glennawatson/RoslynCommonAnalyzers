// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags comparisons that compute a full <c>System.Linq.Enumerable</c> <c>Count()</c> result
/// only to ask whether the sequence has any elements (PSH1119): <c>xs.Count() &gt; 0</c>
/// becomes <c>xs.Any()</c> and <c>xs.Count() == 0</c> becomes <c>!xs.Any()</c>. The comparison
/// shape and the member name gate syntactically before any binding; both operand orders and
/// the zero/one literal forms (<c>&gt; 0</c>, <c>&gt;= 1</c>, <c>!= 0</c>, <c>== 0</c>,
/// <c>&lt; 1</c>, <c>&lt;= 0</c>) are recognized, and the predicate overload qualifies too.
/// A receiver whose static type exposes an accessible constant-time <c>Count</c> or
/// <c>Length</c> property is never reported — that receiver is PSH1103's territory. The rule
/// is resolved once per compilation by probing for <c>System.Linq.Enumerable</c>, so it costs
/// nothing when LINQ is absent.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1119UseAnyOverCountAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The method name the code fix moves emptiness checks to.</summary>
    internal const string AnyMethodName = "Any";

    /// <summary>The count member name the syntax gate accepts.</summary>
    private const string CountMethodName = "Count";

    /// <summary>The metadata name of the LINQ extension-method host type.</summary>
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    /// <summary>The message argument for comparisons that mean the sequence has elements.</summary>
    private const string AnyReplacementText = "Any()";

    /// <summary>The message argument for comparisons that mean the sequence is empty.</summary>
    private const string NegatedAnyReplacementText = "!Any()";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.UseAnyOverCount);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(EnumerableMetadataName) is not { } enumerableType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeComparison(nodeContext, enumerableType),
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression,
                SyntaxKind.GreaterThanExpression,
                SyntaxKind.GreaterThanOrEqualExpression,
                SyntaxKind.LessThanExpression,
                SyntaxKind.LessThanOrEqualExpression);
        });
    }

    /// <summary>Classifies an emptiness-shaped Count() comparison, before any binding.</summary>
    /// <param name="binary">The comparison to inspect.</param>
    /// <returns>The Count invocation and whether the check means "has elements", or <see langword="null"/>.</returns>
    internal static (InvocationExpressionSyntax Invocation, bool HasElements)? TryGetComparisonShape(BinaryExpressionSyntax binary)
    {
        var shape = EmptinessComparisonClassifier.Classify(binary, TryGetCountInvocation(binary.Left), TryGetCountInvocation(binary.Right));
        return shape is { } resolved ? (resolved.Count, resolved.HasElements) : null;
    }

    /// <summary>Reports PSH1119 for an emptiness comparison of an Enumerable Count() result.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type in the current compilation.</param>
    private static void AnalyzeComparison(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerableType)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (TryGetComparisonShape(binary) is not { } shape)
        {
            return;
        }

        if (!IsEnumerableCountExtension(context.SemanticModel, shape.Invocation, enumerableType, context.CancellationToken))
        {
            return;
        }

        var memberAccess = (MemberAccessExpressionSyntax)shape.Invocation.Expression;
        if (shape.Invocation.ArgumentList.Arguments.Count == 0
            && context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type is { } receiverType
            && CollectionReceiverHelper.TryGetCountSourceName(receiverType, out _))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseAnyOverCount,
            binary.SyntaxTree,
            binary.Span,
            shape.HasElements ? AnyReplacementText : NegatedAnyReplacementText));
    }

    /// <summary>Returns an invocation when it is a member-access Count call with at most one argument.</summary>
    /// <param name="expression">The comparison operand.</param>
    /// <returns>The invocation, or <see langword="null"/>.</returns>
    private static InvocationExpressionSyntax? TryGetCountInvocation(ExpressionSyntax expression)
        => expression is InvocationExpressionSyntax { ArgumentList.Arguments.Count: <= 1 } invocation
            && invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == CountMethodName
            ? invocation
            : null;

    /// <summary>Returns whether an invocation binds to a reduced <c>System.Linq.Enumerable</c> extension.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type in the current compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the call is a reduced Enumerable extension.</returns>
    private static bool IsEnumerableCountExtension(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumerableType,
        CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { ReducedFrom: { } reduced }
            && SymbolEqualityComparer.Default.Equals(reduced.ContainingType, enumerableType);
}
