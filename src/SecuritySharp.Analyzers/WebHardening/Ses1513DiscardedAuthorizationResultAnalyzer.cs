// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags an ignored authorization result (SES1513). <c>IAuthorizationService.AuthorizeAsync</c> reports its
/// outcome through the returned <c>AuthorizationResult.Succeeded</c> rather than by throwing, so a call whose
/// result is thrown away authorizes nothing and the guarded code runs regardless. The rule reports an
/// <c>AuthorizeAsync</c> invocation bound to <c>Microsoft.AspNetCore.Authorization.IAuthorizationService</c> --
/// either the interface's own overloads or the convenience extension overloads declared on it -- when the
/// produced value is discarded: the call (optionally <c>await</c>ed, optionally wrapped in
/// <c>.ConfigureAwait(...)</c>) forms an expression statement, or it is assigned to a discard (<c>_ = ...</c>).
/// A call whose result is stored in a variable, returned, passed as an argument, or read (for example
/// <c>.Succeeded</c>) is a legitimate use and is left alone. The rule is resolved once per compilation by
/// probing <c>IAuthorizationService</c>; on a project without ASP.NET Core authorization nothing is registered,
/// so a project that cannot call the API pays nothing. Detection is purely local to the single statement and
/// never traces values across calls.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1513DiscardedAuthorizationResultAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the authorization method whose discarded result is reported.</summary>
    private const string AuthorizeAsyncMethodName = "AuthorizeAsync";

    /// <summary>The name of the <c>Task.ConfigureAwait</c> wrapper skipped when locating the awaited value.</summary>
    private const string ConfigureAwaitMethodName = "ConfigureAwait";

    /// <summary>The identifier text of a discard target (<c>_ = ...</c>).</summary>
    private const string DiscardIdentifier = "_";

    /// <summary>The metadata name of the authorization service the reported call must bind to.</summary>
    private const string AuthorizationServiceMetadataName = "Microsoft.AspNetCore.Authorization.IAuthorizationService";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.DiscardedAuthorizationResult);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var authorizationService = start.Compilation.GetTypeByMetadataName(AuthorizationServiceMetadataName);
            if (authorizationService is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, authorizationService), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1513 for an <c>AuthorizeAsync</c> call on the authorization service whose result is discarded.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="authorizationService">The <c>IAuthorizationService</c> type resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol authorizationService)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.AuthorizeAsync(...)' call whose produced value is thrown away.
        // Neither probe touches the semantic model, so a call that keeps its result costs nothing.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: AuthorizeAsyncMethodName }
            || GetDiscardTarget(invocation) is not { } discardTarget)
        {
            return;
        }

        // Rare path: confirm the call binds to IAuthorizationService.AuthorizeAsync before touching the model further.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: AuthorizeAsyncMethodName } method
            || !IsAuthorizeAsyncOnAuthorizationService(method, authorizationService))
        {
            return;
        }

        // A discard assignment counts only when the left side is a genuine discard, not a variable named '_'.
        if (discardTarget is AssignmentExpressionSyntax discardAssignment
            && context.SemanticModel.GetSymbolInfo(discardAssignment.Left, context.CancellationToken).Symbol is not IDiscardSymbol)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.DiscardedAuthorizationResult,
            invocation.SyntaxTree,
            invocation.Span));
    }

    /// <summary>Returns the node that discards the call's value, or <see langword="null"/> when the value is used.</summary>
    /// <param name="invocation">The <c>AuthorizeAsync</c> invocation.</param>
    /// <returns>
    /// The enclosing expression statement, or the discard assignment, when the produced value is thrown away;
    /// otherwise <see langword="null"/>.
    /// </returns>
    private static SyntaxNode? GetDiscardTarget(InvocationExpressionSyntax invocation)
    {
        var value = ClimbToValueExpression(invocation);
        return value.Parent switch
        {
            ExpressionStatementSyntax statement => statement,
            AssignmentExpressionSyntax assignment when IsDiscardAssignmentOf(assignment, value) => assignment,
            _ => null,
        };
    }

    /// <summary>Returns whether an assignment discards <paramref name="value"/> to a bare <c>_</c> target.</summary>
    /// <param name="assignment">The candidate assignment.</param>
    /// <param name="value">The value expression on the assignment's right side.</param>
    /// <returns><see langword="true"/> for a <c>_ = value</c> simple assignment.</returns>
    private static bool IsDiscardAssignmentOf(AssignmentExpressionSyntax assignment, ExpressionSyntax value)
        => assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && assignment.Right == value
            && assignment.Left is IdentifierNameSyntax { Identifier.ValueText: DiscardIdentifier };

    /// <summary>Climbs from the call to the outermost expression that carries its value.</summary>
    /// <param name="invocation">The <c>AuthorizeAsync</c> invocation.</param>
    /// <returns>The invocation, or the enclosing <c>ConfigureAwait</c> call, <c>await</c>, and parentheses.</returns>
    private static ExpressionSyntax ClimbToValueExpression(InvocationExpressionSyntax invocation)
    {
        ExpressionSyntax current = invocation;

        // A trailing '.ConfigureAwait(...)' wraps the call before the await, so 'await x.AuthorizeAsync(...).ConfigureAwait(false);'
        // is still an ignored result.
        if (current.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: ConfigureAwaitMethodName } configureAccess
            && configureAccess.Expression == current
            && configureAccess.Parent is InvocationExpressionSyntax configureInvocation
            && configureInvocation.Expression == configureAccess)
        {
            current = configureInvocation;
        }

        current = ClimbParentheses(current);

        if (current.Parent is AwaitExpressionSyntax awaitExpression && awaitExpression.Expression == current)
        {
            current = ClimbParentheses(awaitExpression);
        }

        return current;
    }

    /// <summary>Climbs past redundant parentheses wrapping an expression.</summary>
    /// <param name="expression">The inner expression.</param>
    /// <returns>The outermost parenthesized expression around <paramref name="expression"/>.</returns>
    private static ExpressionSyntax ClimbParentheses(ExpressionSyntax expression)
    {
        while (expression.Parent is ParenthesizedExpressionSyntax parenthesized && parenthesized.Expression == expression)
        {
            expression = parenthesized;
        }

        return expression;
    }

    /// <summary>Returns whether a bound method is <c>AuthorizeAsync</c> on the authorization service.</summary>
    /// <param name="method">The bound invocation method symbol.</param>
    /// <param name="authorizationService">The <c>IAuthorizationService</c> type resolved for the compilation.</param>
    /// <returns><see langword="true"/> for an interface overload or a convenience extension declared on it.</returns>
    private static bool IsAuthorizeAsyncOnAuthorizationService(IMethodSymbol method, INamedTypeSymbol authorizationService)
    {
        var definition = method.ReducedFrom ?? method;
        if (SymbolEqualityComparer.Default.Equals(definition.ContainingType, authorizationService))
        {
            return true;
        }

        // The convenience overloads (for example 'AuthorizeAsync(user, policyName)') are extension methods
        // declared on 'this IAuthorizationService'; their first parameter is the service.
        return definition.IsExtensionMethod
            && definition.Parameters.Length > 0
            && SymbolEqualityComparer.Default.Equals(definition.Parameters[0].Type, authorizationService);
    }
}
