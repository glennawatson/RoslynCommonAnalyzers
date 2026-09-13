// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a minimal-API result produced through the untyped <c>Microsoft.AspNetCore.Http.Results</c>
/// factory where the strongly typed <c>TypedResults</c> factory returns the concrete result type
/// (PSH1500). <c>Results.X(...)</c> returns <c>IResult</c> and hides the concrete response shape, so
/// the framework has to infer the endpoint metadata; the matching <c>TypedResults.X(...)</c> returns
/// <c>Ok&lt;T&gt;</c>/<c>NotFound</c>/etc. and describes itself. Each candidate invocation is bound and
/// reported only when the invoked method's containing type is exactly <c>Results</c>. The factory types
/// are resolved only after the invocation passes the syntax check. No automatic code fix, because
/// adopting the typed result can require declaring the handler's return type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1500PreferTypedResultsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The simple name of the untyped factory, used as a bind-free prefilter.</summary>
    private const string ResultsTypeName = "Results";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(AspNetCoreRules.PreferTypedResults);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var markers = new ResultMarkers(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, markers), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1500 for a <c>Results.X(...)</c> call whose typed counterpart exists.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="markers">The compilation's lazily resolved result factories.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, ResultMarkers markers)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || GetReceiverName(memberAccess.Expression) is not { Identifier.ValueText: ResultsTypeName })
        {
            return;
        }

        var types = markers.Get();
        if (types.Length == 0
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { IsStatic: true } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, types[0])
            || !HasMatchingTypedMember(types[1], method.Name))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AspNetCoreRules.PreferTypedResults,
            invocation.SyntaxTree,
            invocation.Span,
            method.Name));
    }

    /// <summary>Returns the simple name that a member-access receiver ends in, for the bind-free prefilter.</summary>
    /// <param name="expression">The receiver expression of the outer member access.</param>
    /// <returns>The rightmost simple name (<c>Results</c> for both <c>Results.X</c> and <c>A.B.Results.X</c>), or <see langword="null"/>.</returns>
    private static SimpleNameSyntax? GetReceiverName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier,
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
        _ => null
    };

    /// <summary>Returns whether <c>TypedResults</c> exposes a static member with the given name.</summary>
    /// <param name="typedResultsType">The resolved <c>TypedResults</c> factory type.</param>
    /// <param name="memberName">The <c>Results</c> member name to match.</param>
    /// <returns><see langword="true"/> when a matching static member exists, so the suggestion is actionable.</returns>
    private static bool HasMatchingTypedMember(INamedTypeSymbol typedResultsType, string memberName)
    {
        var members = typedResultsType.GetMembers(memberName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { IsStatic: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves result factory types on first demand and caches missing factories too.</summary>
    /// <param name="compilation">The compilation whose types are resolved.</param>
    private sealed class ResultMarkers(Compilation compilation)
    {
        /// <summary>The metadata name of the untyped minimal-API result factory.</summary>
        private const string ResultsMetadataName = "Microsoft.AspNetCore.Http.Results";

        /// <summary>The metadata name of the strongly typed minimal-API result factory.</summary>
        private const string TypedResultsMetadataName = "Microsoft.AspNetCore.Http.TypedResults";

        /// <summary>The result factories, or an empty array when either factory is absent.</summary>
        private INamedTypeSymbol[]? _resolved;

        /// <summary>Gets the result factory types, resolving them on first demand.</summary>
        /// <returns>The untyped and typed factories, in that order, or an empty array when either is absent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol[] Get() => _resolved ??= Resolve(compilation);

        /// <summary>Resolves the factory types required for a typed result suggestion.</summary>
        /// <param name="compilation">The compilation whose types are resolved.</param>
        /// <returns>The untyped and typed factories, or an empty array when either is absent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static INamedTypeSymbol[] Resolve(Compilation compilation) =>
            compilation.GetTypeByMetadataName(ResultsMetadataName) is { } resultsType
                && compilation.GetTypeByMetadataName(TypedResultsMetadataName) is { } typedResultsType
                ? [resultsType, typedResultsType]
                : [];
    }
}
