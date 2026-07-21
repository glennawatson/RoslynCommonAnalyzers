// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a fire-and-forget <c>Task.Run</c> inside an MVC controller whose delegate captures the request's
/// <c>HttpContext</c> (SST2707). The context is disposed when the request ends, so the background work reads
/// torn state or throws <c>ObjectDisposedException</c> — and, because the task is discarded, the failure is
/// swallowed.
/// </summary>
/// <remarks>
/// <para>
/// The rule is a heuristic and off by default. It fires only on the discarded shape — the <c>Task.Run(...)</c>
/// invocation is the whole expression of a statement, or the right side of a <c>_ = ...</c> discard assignment
/// — so an awaited, returned, or assigned task is never reported. The delegate must be a lambda or anonymous
/// method that references an expression whose type is (or derives from)
/// <c>Microsoft.AspNetCore.Http.HttpContext</c>, and the enclosing type must derive from
/// <c>Microsoft.AspNetCore.Mvc.ControllerBase</c>.
/// </para>
/// <para>
/// The whole rule is gated at compilation start on both <c>HttpContext</c> and <c>ControllerBase</c> resolving,
/// so a non-web project registers nothing. The clean path is a syntactic shape probe — the invoked name, the
/// discard shape, and a lambda argument — before any binding; the semantic model is consulted only once that
/// shape matches, to confirm the call is <c>System.Threading.Tasks.Task.Run</c> on a controller and that the
/// delegate really closes over an <c>HttpContext</c>-typed value.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2707FireAndForgetHttpContextAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the request context whose capture is reported.</summary>
    private const string HttpContextMetadataName = "Microsoft.AspNetCore.Http.HttpContext";

    /// <summary>The metadata name of the controller base type the rule scopes to.</summary>
    private const string ControllerBaseMetadataName = "Microsoft.AspNetCore.Mvc.ControllerBase";

    /// <summary>The metadata name of the type that owns the offloading helper.</summary>
    private const string TaskMetadataName = "System.Threading.Tasks.Task";

    /// <summary>The name of the offloading helper the rule reports.</summary>
    private const string RunMethodName = "Run";

    /// <summary>The identifier a discard target carries.</summary>
    private const string DiscardName = "_";

    /// <summary>The cached descendant visitor that finds an <c>HttpContext</c>-typed reference in a delegate body.</summary>
    private static readonly DescendantTraversalHelper.DescendantVisitor<ExpressionSyntax, CaptureSearch> CaptureVisitor = VisitExpression;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.FireAndForgetHttpContext);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var compilation = start.Compilation;
            if (compilation.GetTypeByMetadataName(HttpContextMetadataName) is not { } httpContextType
                || compilation.GetTypeByMetadataName(ControllerBaseMetadataName) is not { } controllerBaseType
                || compilation.GetTypeByMetadataName(TaskMetadataName) is not { } taskType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, httpContextType, controllerBaseType, taskType),
                SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports a discarded <c>Task.Run</c> in a controller that captures the request's <c>HttpContext</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="httpContextType">The resolved <c>HttpContext</c> type.</param>
    /// <param name="controllerBaseType">The resolved <c>ControllerBase</c> type.</param>
    /// <param name="taskType">The resolved <c>System.Threading.Tasks.Task</c> type.</param>
    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol httpContextType,
        INamedTypeSymbol controllerBaseType,
        INamedTypeSymbol taskType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a discarded 'Run(...)' call whose single delegate argument is a lambda.
        if (!string.Equals(GetInvokedName(invocation.Expression), RunMethodName, StringComparison.Ordinal)
            || !IsDiscarded(invocation)
            || GetDelegateBody(invocation) is not { } body)
        {
            return;
        }

        var typeDeclaration = invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (typeDeclaration is null
            || !IsOrDerivesFrom(context.SemanticModel.GetDeclaredSymbol(typeDeclaration, context.CancellationToken), controllerBaseType))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: RunMethodName, ContainingType: { } container }
            || !SymbolEqualityComparer.Default.Equals(container, taskType))
        {
            return;
        }

        if (!CapturesHttpContext(body, context.SemanticModel, httpContextType, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(FrameworksRules.FireAndForgetHttpContext, invocation.GetLocation()));
    }

    /// <summary>Returns the invoked member's simple name for an <c>Identifier(...)</c> or <c>x.Identifier(...)</c> call.</summary>
    /// <param name="expression">The invocation's callee expression.</param>
    /// <returns>The simple name, or <see langword="null"/> when the callee is not a plain member reference.</returns>
    private static string? GetInvokedName(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns whether the invocation's result is thrown away rather than awaited, returned, or stored.</summary>
    /// <param name="invocation">The <c>Task.Run</c> invocation.</param>
    /// <returns><see langword="true"/> for a bare statement call or a <c>_ = ...</c> discard assignment.</returns>
    private static bool IsDiscarded(InvocationExpressionSyntax invocation)
        => invocation.Parent switch
        {
            ExpressionStatementSyntax => true,
            AssignmentExpressionSyntax assignment
                when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    && assignment.Right == invocation
                    && assignment.Left is IdentifierNameSyntax { Identifier.ValueText: DiscardName }
                    && assignment.Parent is ExpressionStatementSyntax => true,
            _ => false,
        };

    /// <summary>Returns the body of the invocation's single delegate argument when it is a lambda or anonymous method.</summary>
    /// <param name="invocation">The <c>Task.Run</c> invocation.</param>
    /// <returns>The delegate body, or <see langword="null"/> when the first argument is not an inline delegate.</returns>
    private static CSharpSyntaxNode? GetDelegateBody(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 1)
        {
            return null;
        }

        return arguments[0].Expression switch
        {
            SimpleLambdaExpressionSyntax lambda => lambda.Body,
            ParenthesizedLambdaExpressionSyntax lambda => lambda.Body,
            AnonymousMethodExpressionSyntax anonymous => anonymous.Body,
            _ => null,
        };
    }

    /// <summary>Returns whether a delegate body references an expression typed as (or derived from) <c>HttpContext</c>.</summary>
    /// <param name="body">The delegate body: an expression or a block.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="httpContextType">The resolved <c>HttpContext</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a captured <c>HttpContext</c>-typed reference is found.</returns>
    private static bool CapturesHttpContext(CSharpSyntaxNode body, SemanticModel model, INamedTypeSymbol httpContextType, CancellationToken cancellationToken)
    {
        if (body is ExpressionSyntax expression && IsHttpContextTyped(expression, model, httpContextType, cancellationToken))
        {
            return true;
        }

        var search = new CaptureSearch(model, httpContextType, cancellationToken);
        DescendantTraversalHelper.VisitDescendants(body, ref search, CaptureVisitor);
        return search.Found;
    }

    /// <summary>The descendant visitor: stops as soon as an <c>HttpContext</c>-typed reference is seen.</summary>
    /// <param name="node">The current descendant expression.</param>
    /// <param name="state">The threaded search state.</param>
    /// <returns><see langword="false"/> to stop once a match is found; otherwise <see langword="true"/>.</returns>
    private static bool VisitExpression(ExpressionSyntax node, ref CaptureSearch state) => state.Observe(node);

    /// <summary>Returns whether an expression's type is, or derives from, <c>HttpContext</c>.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="httpContextType">The resolved <c>HttpContext</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the expression is <c>HttpContext</c>-typed.</returns>
    private static bool IsHttpContextTyped(ExpressionSyntax expression, SemanticModel model, INamedTypeSymbol httpContextType, CancellationToken cancellationToken)
        => IsOrDerivesFrom(model.GetTypeInfo(expression, cancellationToken).Type, httpContextType);

    /// <summary>Returns whether a type is, or derives from, a given base type.</summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="baseType">The base type to test against.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> is <paramref name="baseType"/> or a subtype of it.</returns>
    private static bool IsOrDerivesFrom(ITypeSymbol? type, INamedTypeSymbol baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The state threaded through the delegate-body descendant walk.</summary>
    private sealed class CaptureSearch
    {
        /// <summary>The semantic model used to resolve each expression's type.</summary>
        private readonly SemanticModel _model;

        /// <summary>The resolved <c>HttpContext</c> type to match against.</summary>
        private readonly INamedTypeSymbol _httpContextType;

        /// <summary>A token that cancels the walk.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>Initializes a new instance of the <see cref="CaptureSearch"/> class.</summary>
        /// <param name="model">The semantic model.</param>
        /// <param name="httpContextType">The resolved <c>HttpContext</c> type.</param>
        /// <param name="cancellationToken">A token that cancels the walk.</param>
        public CaptureSearch(SemanticModel model, INamedTypeSymbol httpContextType, CancellationToken cancellationToken)
        {
            _model = model;
            _httpContextType = httpContextType;
            _cancellationToken = cancellationToken;
        }

        /// <summary>Gets a value indicating whether an <c>HttpContext</c>-typed reference has been seen.</summary>
        public bool Found { get; private set; }

        /// <summary>Records whether a visited expression denotes an <c>HttpContext</c>-typed value.</summary>
        /// <param name="node">The current descendant expression.</param>
        /// <returns><see langword="false"/> to stop the walk once a match is found; otherwise <see langword="true"/>.</returns>
        public bool Observe(ExpressionSyntax node)
        {
            // Only a simple name or a member access can denote an HttpContext-typed value; skip everything else.
            if (node is not (IdentifierNameSyntax or MemberAccessExpressionSyntax)
                || !IsHttpContextTyped(node, _model, _httpContextType, _cancellationToken))
            {
                return true;
            }

            Found = true;
            return false;
        }
    }
}
