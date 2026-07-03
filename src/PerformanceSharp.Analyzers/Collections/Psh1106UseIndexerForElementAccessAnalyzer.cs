// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests indexing list-like receivers instead of calling the
/// <c>System.Linq.Enumerable</c> <c>First()</c>, <c>Last()</c>, and
/// <c>ElementAt(index)</c> extensions (PSH1106). An invocation qualifies only when it
/// is the member-access extension form, binds to <c>System.Linq.Enumerable</c>, and the
/// receiver's static type is indexable: a rank-1 array, <see cref="string"/>, or a type
/// that is or implements <c>IList&lt;T&gt;</c>/<c>IReadOnlyList&lt;T&gt;</c>.
/// <c>Last()</c> is additionally gated on the static type exposing a constant-time
/// <c>Count</c>/<c>Length</c>, because its rewrite reads the count. The rule is resolved
/// once per compilation by probing for <c>System.Linq.Enumerable</c>, so it costs
/// nothing when LINQ is absent.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1106UseIndexerForElementAccessAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key carrying the count property name used by the <c>Last()</c> code fix.</summary>
    internal const string CountSourceKey = "CountSource";

    /// <summary>The metadata name of the LINQ extension-method host type.</summary>
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    /// <summary>The unreduced parameter count of Enumerable extensions taking only the source.</summary>
    private const int SourceOnlyParameterCount = 1;

    /// <summary>The unreduced parameter count of Enumerable extensions taking the source and an index.</summary>
    private const int SourceAndIndexParameterCount = 2;

    /// <summary>Cached diagnostic properties naming Count as the receiver's count source.</summary>
    private static readonly ImmutableDictionary<string, string?> CountProperties =
        ImmutableDictionary<string, string?>.Empty.Add(CountSourceKey, CollectionReceiverHelper.CountPropertyName);

    /// <summary>Cached diagnostic properties naming Length as the receiver's count source.</summary>
    private static readonly ImmutableDictionary<string, string?> LengthProperties =
        ImmutableDictionary<string, string?>.Empty.Add(CountSourceKey, CollectionReceiverHelper.LengthPropertyName);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.UseIndexerForElementAccess);

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

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, enumerableType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1106 for an Enumerable element-access call on an indexable receiver.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type in the current compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerableType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression))
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        var parameterCount = GetTargetParameterCount(methodName);
        if (parameterCount == 0 || invocation.ArgumentList.Arguments.Count != parameterCount - 1)
        {
            return;
        }

        if (!IsEnumerableExtension(context.SemanticModel, invocation, enumerableType, parameterCount, context.CancellationToken))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type is not { } receiverType
            || !CollectionReceiverHelper.IsListLike(receiverType))
        {
            return;
        }

        if (methodName == "Last")
        {
            ReportLast(context, invocation, receiverType, methodName);
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseIndexerForElementAccess,
            invocation.SyntaxTree,
            invocation.Span,
            methodName));
    }

    /// <summary>Reports a <c>Last()</c> call when the receiver's static type also has a constant-time count.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The qualifying invocation.</param>
    /// <param name="receiverType">The receiver's static type.</param>
    /// <param name="methodName">The invoked method name.</param>
    private static void ReportLast(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol receiverType,
        string methodName)
    {
        if (!CollectionReceiverHelper.TryGetCountSourceName(receiverType, out var countSource))
        {
            return;
        }

        var properties = countSource == CollectionReceiverHelper.LengthPropertyName ? LengthProperties : CountProperties;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseIndexerForElementAccess,
            invocation.SyntaxTree,
            invocation.Span,
            properties,
            methodName));
    }

    /// <summary>Maps a candidate method name to its Enumerable parameter count, or zero when not a target.</summary>
    /// <param name="methodName">The invoked method name.</param>
    /// <returns>The expected unreduced parameter count, or zero for non-target names.</returns>
    private static int GetTargetParameterCount(string methodName)
        => methodName switch
        {
            "First" or "Last" => SourceOnlyParameterCount,
            "ElementAt" => SourceAndIndexParameterCount,
            _ => 0
        };

    /// <summary>Returns whether an invocation binds to an Enumerable extension with the expected parameter count.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type in the current compilation.</param>
    /// <param name="parameterCount">The expected unreduced parameter count.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the call is a reduced Enumerable extension of the expected shape.</returns>
    private static bool IsEnumerableExtension(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumerableType,
        int parameterCount,
        CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { ReducedFrom: { } reduced }
            && reduced.Parameters.Length == parameterCount
            && SymbolEqualityComparer.Default.Equals(reduced.ContainingType, enumerableType);
}
