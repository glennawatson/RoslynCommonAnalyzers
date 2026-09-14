// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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

    /// <summary>The metadata name of the untyped minimal-API result factory.</summary>
    private const string ResultsMetadataName = "Microsoft.AspNetCore.Http.Results";

    /// <summary>The metadata name of the strongly typed minimal-API result factory.</summary>
    private const string TypedResultsMetadataName = "Microsoft.AspNetCore.Http.TypedResults";

    /// <summary>The untyped and typed result factory metadata names, in slot order.</summary>
    private static readonly string[] ResultFactoryMetadataNames = [ResultsMetadataName, TypedResultsMetadataName];

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(AspNetCoreRules.PreferTypedResults);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataTypes(compilation, ResultFactoryMetadataNames),
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports PSH1500 for a <c>Results.X(...)</c> call whose typed counterpart exists.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="markers">The compilation's lazily resolved result factories.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, LazyMetadataTypes markers)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || GetReceiverName(memberAccess.Expression) is not { Identifier.ValueText: ResultsTypeName })
        {
            return;
        }

        if (markers.Get() is not [{ } resultsType, { } typedResultsType]
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { IsStatic: true } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, resultsType)
            || !SymbolFacts.HasStaticMethod(typedResultsType, method.Name))
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
}
