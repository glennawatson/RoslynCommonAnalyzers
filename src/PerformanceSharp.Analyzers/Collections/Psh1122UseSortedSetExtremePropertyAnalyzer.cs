// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags the parameterless <c>Enumerable.Min()</c> and <c>Enumerable.Max()</c> extensions called on a
/// <c>SortedSet&lt;T&gt;</c> or an <c>ImmutableSortedSet&lt;T&gt;</c> (PSH1122). Those types keep their
/// elements in order, so the smallest and the largest are already sitting at the ends and the
/// <c>Min</c>/<c>Max</c> properties return them in constant time; the LINQ extension knows nothing
/// about the receiver and walks every element to rediscover what the set already knows.
/// </summary>
/// <remarks>
/// The rule matches on the invoked name before it binds anything, and it only reports the overloads
/// whose single parameter is the source — one that takes a selector or a comparer is asking a
/// different question and is left alone. The call must bind to <c>System.Linq.Enumerable</c>, which
/// keeps <c>Queryable</c> out, and the receiver's <em>static</em> type must be the sorted set: the
/// property does not exist on the interfaces it implements.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1122UseSortedSetExtremePropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The minimum member name, shared by the extension and the property.</summary>
    internal const string MinMemberName = "Min";

    /// <summary>The maximum member name, shared by the extension and the property.</summary>
    internal const string MaxMemberName = "Max";

    /// <summary>How the minimum extension call is written into the message.</summary>
    private const string MinCallText = "Min()";

    /// <summary>How the maximum extension call is written into the message.</summary>
    private const string MaxCallText = "Max()";

    /// <summary>How the minimum element is described in the message.</summary>
    private const string SmallestText = "smallest";

    /// <summary>How the maximum element is described in the message.</summary>
    private const string LargestText = "largest";

    /// <summary>The metadata name of the LINQ extension class.</summary>
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.UseSortedSetExtremeProperty);

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

    /// <summary>Returns whether an invocation is a parameterless <c>Min</c>/<c>Max</c> member call, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the call has the extreme-extension shape.</returns>
    internal static bool IsExtremeExtensionShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && memberAccess.Name.Identifier.ValueText is MinMemberName or MaxMemberName;

    /// <summary>Reports PSH1122 for a sorted set whose extreme element is fetched through LINQ.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="enumerableType">The LINQ extension class.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerableType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsExtremeExtensionShape(invocation))
        {
            return;
        }

        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        if (!IsSourceOnlyEnumerableExtension(context, invocation, enumerableType)
            || !IsSortedSetReceiver(context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type))
        {
            return;
        }

        var isMinimum = memberAccess.Name.Identifier.ValueText == MinMemberName;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseSortedSetExtremeProperty,
            memberAccess.Name.GetLocation(),
            isMinimum ? MinCallText : MaxCallText,
            isMinimum ? MinMemberName : MaxMemberName,
            isMinimum ? SmallestText : LargestText));
    }

    /// <summary>Returns whether an invocation binds to an Enumerable extension whose only parameter is the source.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="enumerableType">The LINQ extension class.</param>
    /// <returns><see langword="true"/> when the call is a reduced source-only Enumerable extension.</returns>
    private static bool IsSourceOnlyEnumerableExtension(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumerableType)
        => context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol { ReducedFrom: { Parameters.Length: 1 } reduced }
            && SymbolEqualityComparer.Default.Equals(reduced.ContainingType, enumerableType);

    /// <summary>Returns whether a receiver's static type keeps its elements in sorted order.</summary>
    /// <param name="type">The receiver's static type.</param>
    /// <returns><see langword="true"/> for <c>SortedSet&lt;T&gt;</c> and <c>ImmutableSortedSet&lt;T&gt;</c>.</returns>
    private static bool IsSortedSetReceiver(ITypeSymbol? type)
        => type is INamedTypeSymbol { OriginalDefinition: { Arity: 1 } definition }
            && (IsSortedSet(definition) || IsImmutableSortedSet(definition));

    /// <summary>Returns whether a type is <c>System.Collections.Generic.SortedSet&lt;T&gt;</c>.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for the mutable sorted set.</returns>
    private static bool IsSortedSet(INamedTypeSymbol type)
        => type.Name == "SortedSet" && IsInSystemCollections(type.ContainingNamespace, "Generic");

    /// <summary>Returns whether a type is <c>System.Collections.Immutable.ImmutableSortedSet&lt;T&gt;</c>.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for the immutable sorted set.</returns>
    private static bool IsImmutableSortedSet(INamedTypeSymbol type)
        => type.Name == "ImmutableSortedSet" && IsInSystemCollections(type.ContainingNamespace, "Immutable");

    /// <summary>Returns whether a namespace is the named child of <c>System.Collections</c>.</summary>
    /// <param name="containing">The type's containing namespace.</param>
    /// <param name="leaf">The expected leaf namespace name.</param>
    /// <returns><see langword="true"/> when the namespace is <c>System.Collections.{leaf}</c>.</returns>
    private static bool IsInSystemCollections(INamespaceSymbol? containing, string leaf)
        => containing is not null
            && containing.Name == leaf
            && containing.ContainingNamespace is { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } };
}
