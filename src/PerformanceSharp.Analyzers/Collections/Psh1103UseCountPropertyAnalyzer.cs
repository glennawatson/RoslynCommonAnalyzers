// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests reading a collection's own <c>Count</c>/<c>Length</c> property instead of
/// calling the parameterless <c>System.Linq.Enumerable</c> <c>Count()</c> and <c>Any()</c>
/// extensions (PSH1103). An invocation qualifies only when it is the member-access
/// extension form with an empty argument list, binds to a source-only
/// <c>System.Linq.Enumerable</c> method, and the receiver's static type exposes an
/// accessible constant-time <see cref="int"/> count — directly on the type or a base
/// type, or via <c>ICollection&lt;T&gt;</c>/<c>IReadOnlyCollection&lt;T&gt;</c> for
/// interface and type-parameter receivers. The rule is resolved once per compilation by
/// probing for <c>System.Linq.Enumerable</c>, so it costs nothing when LINQ is absent.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1103UseCountPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key carrying the suggested count property name for the code fix.</summary>
    internal const string PropertyNameKey = "PropertyName";

    /// <summary>The metadata name of the LINQ extension-method host type.</summary>
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    /// <summary>Cached diagnostic properties suggesting the Count property.</summary>
    private static readonly ImmutableDictionary<string, string?> CountProperties =
        ImmutableDictionary<string, string?>.Empty.Add(PropertyNameKey, CollectionReceiverHelper.CountPropertyName);

    /// <summary>Cached diagnostic properties suggesting the Length property.</summary>
    private static readonly ImmutableDictionary<string, string?> LengthProperties =
        ImmutableDictionary<string, string?>.Empty.Add(PropertyNameKey, CollectionReceiverHelper.LengthPropertyName);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.UseCountProperty);

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

    /// <summary>Reports PSH1103 for a parameterless Enumerable Count/Any call whose receiver has a constant-time count.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type in the current compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerableType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || memberAccess.Name.Identifier.ValueText is not ("Count" or "Any"))
        {
            return;
        }

        if (!IsSourceOnlyEnumerableExtension(context.SemanticModel, invocation, enumerableType, context.CancellationToken))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type is not { } receiverType
            || !CollectionReceiverHelper.TryGetCountSourceName(receiverType, out var propertyName))
        {
            return;
        }

        var properties = propertyName == CollectionReceiverHelper.LengthPropertyName ? LengthProperties : CountProperties;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseCountProperty,
            invocation.SyntaxTree,
            invocation.Span,
            properties,
            propertyName));
    }

    /// <summary>Returns whether an invocation binds to an Enumerable extension whose only parameter is the source.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type in the current compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the call is a reduced source-only Enumerable extension.</returns>
    private static bool IsSourceOnlyEnumerableExtension(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumerableType,
        CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { ReducedFrom: { Parameters.Length: 1 } reduced }
            && SymbolEqualityComparer.Default.Equals(reduced.ContainingType, enumerableType);
}
