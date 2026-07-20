// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a single CORS policy that calls both <c>AllowAnyOrigin</c> and <c>AllowCredentials</c> on the same
/// <c>Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder</c> (SES1501). The rule triggers on an
/// <c>AllowCredentials()</c> invocation whose bound method's containing type is <c>CorsPolicyBuilder</c>, then
/// scans the enclosing policy scope -- the configuration lambda body passed to <c>AddPolicy</c>/<c>AddDefaultPolicy</c>,
/// or, for a bare fluent chain, the single enclosing statement -- for an <c>AllowAnyOrigin()</c> call on
/// <c>CorsPolicyBuilder</c>. Both member symbols are bound so a same-named method on an unrelated type is never
/// matched. The scan is a purely local ancestor/descendant walk: no data flow, and cross-statement uses outside a
/// policy lambda are deliberately left alone. <c>CorsPolicyBuilder</c> is probed once per compilation; a project
/// without ASP.NET Core CORS registers nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1501CorsAnyOriginWithCredentialsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the credential-allowing fluent method that triggers the rule.</summary>
    private const string AllowCredentialsMethodName = "AllowCredentials";

    /// <summary>The name of the any-origin fluent method whose presence completes the violation.</summary>
    private const string AllowAnyOriginMethodName = "AllowAnyOrigin";

    /// <summary>The metadata name of the CORS policy builder that gates the rule.</summary>
    private const string CorsPolicyBuilderMetadataName = "Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.CorsAnyOriginWithCredentials);

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

    /// <summary>Reports SES1501 for an <c>AllowCredentials()</c> call whose policy scope also allows any origin.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="builderType">The gated <c>CorsPolicyBuilder</c> type resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol builderType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member-access '.AllowCredentials()' call. The receiver is required, so an
        // unqualified identifier can never reach the instance method and is ignored.
        if (GetCalleeName(invocation.Expression) is not { Identifier.ValueText: AllowCredentialsMethodName } credentialsName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: AllowCredentialsMethodName } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, builderType)
            || GetPolicyScope(invocation) is not { } scope
            || !ScopeAllowsAnyOrigin(scope, context.SemanticModel, builderType, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.CorsAnyOriginWithCredentials,
            invocation.SyntaxTree,
            TextSpan.FromBounds(credentialsName.SpanStart, invocation.Span.End)));
    }

    /// <summary>Returns the enclosing policy scope to scan: the nearest lambda body, else the nearest statement or single-expression clause.</summary>
    /// <param name="node">The reported <c>AllowCredentials()</c> invocation.</param>
    /// <returns>The scope node to search for <c>AllowAnyOrigin()</c>, or <see langword="null"/> when none is found.</returns>
    private static SyntaxNode? GetPolicyScope(SyntaxNode node)
    {
        SyntaxNode? fallbackScope = null;
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            // A configuration lambda ('policy => ...') is the CORS policy body: prefer it over any
            // intervening statement so a multi-statement block body is scanned in full.
            if (ancestor is AnonymousFunctionExpressionSyntax lambda)
            {
                return lambda.Body;
            }

            // Outside a lambda the fluent chain lives in one local unit: a statement, an expression-bodied
            // member ('=> chain'), or an initializer ('= chain'). The nearest such unit is the scope.
            fallbackScope ??= ancestor is StatementSyntax or ArrowExpressionClauseSyntax or EqualsValueClauseSyntax ? ancestor : null;

            // No lambda encloses the call once a declaration boundary is reached.
            if (ancestor is MemberDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                break;
            }
        }

        return fallbackScope;
    }

    /// <summary>Returns whether the scope contains an <c>AllowAnyOrigin()</c> call on the gated builder.</summary>
    /// <param name="scope">The policy scope to search.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="builderType">The gated <c>CorsPolicyBuilder</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a matching <c>AllowAnyOrigin()</c> call is present.</returns>
    private static bool ScopeAllowsAnyOrigin(SyntaxNode scope, SemanticModel model, INamedTypeSymbol builderType, CancellationToken cancellationToken)
    {
        // An expression-lambda body can itself be the 'AllowAnyOrigin()' call (a reversed chain,
        // 'policy => policy.AllowCredentials().AllowAnyOrigin()'); the descendant walk skips its own root,
        // so the scope node is tested first.
        if (scope is InvocationExpressionSyntax rootInvocation && IsAllowAnyOriginCall(rootInvocation, model, builderType, cancellationToken))
        {
            return true;
        }

        var scan = new AllowAnyOriginScan(model, builderType, false, cancellationToken);
        DescendantTraversalHelper.VisitDescendants<InvocationExpressionSyntax, AllowAnyOriginScan>(
            scope,
            ref scan,
            static (InvocationExpressionSyntax invocation, ref AllowAnyOriginScan state) =>
            {
                if (!IsAllowAnyOriginCall(invocation, state.Model, state.Builder, state.CancellationToken))
                {
                    return true;
                }

                state.Found = true;
                return false;
            });

        return scan.Found;
    }

    /// <summary>Returns whether an invocation is <c>AllowAnyOrigin()</c> bound to the gated builder.</summary>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="builderType">The gated <c>CorsPolicyBuilder</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for an <c>AllowAnyOrigin()</c> call on the gated builder.</returns>
    private static bool IsAllowAnyOriginCall(InvocationExpressionSyntax invocation, SemanticModel model, INamedTypeSymbol builderType, CancellationToken cancellationToken)
        => GetCalleeName(invocation.Expression) is { Identifier.ValueText: AllowAnyOriginMethodName }
            && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { Name: AllowAnyOriginMethodName } method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, builderType);

    /// <summary>Returns the simple name a member invocation targets, or <see langword="null"/> when there is no receiver.</summary>
    /// <param name="invoked">The invocation's callee expression.</param>
    /// <returns>The invoked member's simple name, or <see langword="null"/> when it is not a member access.</returns>
    private static SimpleNameSyntax? GetCalleeName(ExpressionSyntax invoked)
        => invoked switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
            _ => null,
        };

    /// <summary>Threads the binding inputs and the found flag through the <c>AllowAnyOrigin()</c> descendant scan.</summary>
    /// <param name="Model">The semantic model used to bind candidate invocations.</param>
    /// <param name="Builder">The gated <c>CorsPolicyBuilder</c> type.</param>
    /// <param name="Found">Whether a matching <c>AllowAnyOrigin()</c> call has been found.</param>
    /// <param name="CancellationToken">A token that cancels the binding.</param>
    private record struct AllowAnyOriginScan(SemanticModel Model, INamedTypeSymbol Builder, bool Found, CancellationToken CancellationToken);
}
