// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a CORS origin predicate that unconditionally allows every origin (SES1502). The rule reports the
/// predicate argument of <c>CorsPolicyBuilder.SetIsOriginAllowed(...)</c> when that predicate always returns
/// <see langword="true"/>: an expression lambda <c>_ =&gt; true</c> / <c>origin =&gt; true</c>, a block lambda
/// whose only reachable result is <c>return true;</c>, or a method group to a source method of that shape.
/// An always-true predicate accepts every origin, which is equivalent to <c>AllowAnyOrigin</c> and — combined
/// with credentials — leaks credentialed cross-origin responses to any site. The predicate body is inspected
/// only locally (the expression body, or the block's own <c>return</c> statements); no value that flows in
/// from elsewhere is followed. The rule resolves <c>Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder</c>
/// once per compilation and registers nothing when it is absent, so a project without ASP.NET Core CORS pays
/// nothing and never receives a diagnostic it cannot act on. The invoked method is bound to confirm it is
/// <c>SetIsOriginAllowed</c> on that type, so a same-named method on an unrelated type is never flagged.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1502AlwaysAllowedCorsOriginAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the origin-predicate method whose argument is inspected.</summary>
    private const string SetIsOriginAllowedMethodName = "SetIsOriginAllowed";

    /// <summary>The metadata name of the CORS policy builder that owns the guarded method.</summary>
    private const string CorsPolicyBuilderMetadataName = "Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder";

    /// <summary>The reportable shapes a <c>SetIsOriginAllowed</c> predicate argument can take.</summary>
    private enum PredicateShape
    {
        /// <summary>Not a reportable predicate shape.</summary>
        None,

        /// <summary>A lambda or anonymous method already known to always return true.</summary>
        AlwaysTrueLambda,

        /// <summary>A method group whose referenced method still needs to be inspected.</summary>
        MethodGroup,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.AlwaysAllowedCorsOrigin);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var builderType = start.Compilation.GetTypeByMetadataName(CorsPolicyBuilderMetadataName);
            if (builderType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, builderType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1502 for a <c>SetIsOriginAllowed</c> call whose predicate always returns true.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="builderType">The gated <c>CorsPolicyBuilder</c> type resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol builderType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.SetIsOriginAllowed(<single argument>)' call.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: SetIsOriginAllowedMethodName }
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        // A lambda's always-true shape is decided syntactically here, so an origin-checking predicate is
        // rejected before the semantic model is touched. A method group needs binding to find its declaration.
        var predicate = invocation.ArgumentList.Arguments[0].Expression;
        var shape = ClassifyPredicate(predicate);
        if (shape == PredicateShape.None)
        {
            return;
        }

        // Bind the call: report only when it truly resolves to SetIsOriginAllowed on CorsPolicyBuilder.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: SetIsOriginAllowedMethodName } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, builderType))
        {
            return;
        }

        // A method group is always-true only when its referenced source method is.
        if (shape == PredicateShape.MethodGroup && !AlwaysTrueCallback.IsAlwaysTrueMethodGroup(context.SemanticModel, predicate, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.AlwaysAllowedCorsOrigin,
            predicate.SyntaxTree,
            predicate.Span));
    }

    /// <summary>Classifies a predicate argument into the shape that lets it be reported.</summary>
    /// <param name="predicate">The single argument passed to <c>SetIsOriginAllowed</c>.</param>
    /// <returns>The predicate shape: an always-true lambda, a bindable method group, or neither.</returns>
    private static PredicateShape ClassifyPredicate(ExpressionSyntax predicate)
    {
        if (predicate is AnonymousFunctionExpressionSyntax function)
        {
            return AlwaysTrueCallback.IsAlwaysTrueLambda(function) ? PredicateShape.AlwaysTrueLambda : PredicateShape.None;
        }

        return predicate is IdentifierNameSyntax or MemberAccessExpressionSyntax ? PredicateShape.MethodGroup : PredicateShape.None;
    }
}
