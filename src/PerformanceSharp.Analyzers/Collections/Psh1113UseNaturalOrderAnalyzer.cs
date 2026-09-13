// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>OrderBy(x =&gt; x)</c> and <c>OrderByDescending(x =&gt; x)</c> on
/// <c>System.Linq.Enumerable</c> (PSH1113), where <c>Order()</c>/<c>OrderDescending()</c>
/// compare elements directly without a key-selector delegate. The whole rule is gated on
/// <c>Enumerable.Order</c> existing in the compilation (.NET 7+), so it costs nothing on older
/// frameworks. A trailing comparer argument is preserved by the code fix; <c>Queryable</c>
/// sources are skipped because rewriting expression trees changes what a provider sees.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1113UseNaturalOrderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ascending sort method name.</summary>
    internal const string OrderByMethodName = "OrderBy";

    /// <summary>The descending sort method name.</summary>
    internal const string OrderByDescendingMethodName = "OrderByDescending";

    /// <summary>The ascending replacement method name.</summary>
    internal const string OrderMethodName = "Order";

    /// <summary>The descending replacement method name.</summary>
    internal const string OrderDescendingMethodName = "OrderDescending";

    /// <summary>The argument count of the sort overload that carries a comparer.</summary>
    internal const int SelectorAndComparerArgumentCount = 2;

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CollectionRules.UseNaturalOrder);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var enumerableSymbols = new EnumerableSymbols(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, enumerableSymbols), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation has the identity-selector sort shape, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when an OrderBy/OrderByDescending call passes an identity lambda first.</returns>
    internal static bool IsIdentitySortShape(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax access
            && access.Name.Identifier.ValueText is OrderByMethodName or OrderByDescendingMethodName
            && invocation.ArgumentList.Arguments.Count is 1 or SelectorAndComparerArgumentCount
            && IsIdentityLambda(invocation.ArgumentList.Arguments[0].Expression);

    /// <summary>Returns whether an expression is a lambda that returns its own single parameter.</summary>
    /// <param name="expression">The candidate selector expression.</param>
    /// <returns><see langword="true"/> for <c>x =&gt; x</c> in simple or parenthesized form.</returns>
    private static bool IsIdentityLambda(ExpressionSyntax expression) =>
        expression switch
        {
            SimpleLambdaExpressionSyntax simple =>
                simple.ExpressionBody is IdentifierNameSyntax body
                    && body.Identifier.ValueText == simple.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesized =>
                parenthesized.ParameterList.Parameters.Count == 1
                    && parenthesized.ExpressionBody is IdentifierNameSyntax body
                    && body.Identifier.ValueText == parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
            _ => false,
        };

    /// <summary>Reports PSH1113 for an identity-selector sort that binds to the LINQ extension class.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="enumerableSymbols">The LINQ extension class, resolved only for an identity-sort candidate.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, EnumerableSymbols enumerableSymbols)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsIdentitySortShape(invocation)
            || enumerableSymbols.Get() is not { } enumerableType
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, enumerableType))
        {
            return;
        }

        var name = ((MemberAccessExpressionSyntax)invocation.Expression).Name;
        var isDescending = name.Identifier.ValueText == OrderByDescendingMethodName;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseNaturalOrder,
            name.GetLocation(),
            isDescending ? OrderDescendingMethodName : OrderMethodName,
            name.Identifier.ValueText));
    }

    /// <summary>Resolves natural-order support once, after the first identity-sort candidate.</summary>
    /// <param name="compilation">The compilation whose LINQ API is probed.</param>
    private sealed class EnumerableSymbols(Compilation compilation)
    {
        /// <summary>The metadata name of the LINQ extension class.</summary>
        private const string EnumerableMetadataName = "System.Linq.Enumerable";

        /// <summary>Serializes the first resolution across concurrent invocation callbacks.</summary>
        private readonly object _gate = new();

        /// <summary>The published resolution, including an unavailable LINQ API.</summary>
        private EnumerableResolution? _resolved;

        /// <summary>Gets the LINQ extension class when it provides natural ordering.</summary>
        /// <returns>The extension class, or null when Enumerable.Order is unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (Volatile.Read(ref _resolved) ?? Resolve()).Type;

        /// <summary>Builds and publishes the framework probe without locking subsequent reads.</summary>
        /// <returns>The cached resolution, including a missing replacement API.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private EnumerableResolution Resolve()
        {
            lock (_gate)
            {
                var resolved = _resolved;
                if (resolved is null)
                {
                    var enumerableType = compilation.GetTypeByMetadataName(EnumerableMetadataName);
                    resolved = new(
                        enumerableType is not null && !enumerableType.GetMembers(OrderMethodName).IsEmpty
                            ? enumerableType
                            : null);
                    Volatile.Write(ref _resolved, resolved);
                }

                return resolved;
            }
        }

        /// <summary>Stores the complete probe result, including the absence of natural ordering.</summary>
        /// <param name="Type">The LINQ extension class, or null when the replacement is unavailable.</param>
        private sealed record EnumerableResolution(INamedTypeSymbol? Type);
    }
}
