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
        if (shape == PredicateShape.MethodGroup && !IsAlwaysTrueMethodGroup(context.SemanticModel, predicate, context.CancellationToken))
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
            return IsAlwaysTrueLambda(function) ? PredicateShape.AlwaysTrueLambda : PredicateShape.None;
        }

        return predicate is IdentifierNameSyntax or MemberAccessExpressionSyntax ? PredicateShape.MethodGroup : PredicateShape.None;
    }

    /// <summary>Returns whether a lambda or anonymous method always yields <see langword="true"/>.</summary>
    /// <param name="function">The predicate lambda or anonymous method.</param>
    /// <returns><see langword="true"/> when the body is <c>=&gt; true</c> or a block whose only result is <c>return true;</c>.</returns>
    private static bool IsAlwaysTrueLambda(AnonymousFunctionExpressionSyntax function)
        => IsAlwaysTrueBody(function.ExpressionBody, function.Block);

    /// <summary>Returns whether the method a method group references always yields <see langword="true"/>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="predicate">The method-group argument expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the referenced source method always returns true.</returns>
    private static bool IsAlwaysTrueMethodGroup(SemanticModel model, ExpressionSyntax predicate, CancellationToken cancellationToken)
    {
        // A method group without exactly one source declaration (metadata, partial, or overload set) cannot be
        // inspected locally, so it is left alone.
        if (model.GetSymbolInfo(predicate, cancellationToken).Symbol is not IMethodSymbol method
            || method.DeclaringSyntaxReferences.Length != 1)
        {
            return false;
        }

        return method.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) switch
        {
            MethodDeclarationSyntax declaration => IsAlwaysTrueBody(declaration.ExpressionBody?.Expression, declaration.Body),
            LocalFunctionStatementSyntax localFunction => IsAlwaysTrueBody(localFunction.ExpressionBody?.Expression, localFunction.Body),
            _ => false,
        };
    }

    /// <summary>Returns whether a member body always yields <see langword="true"/>.</summary>
    /// <param name="expressionBody">The arrow-body expression, when the member is expression-bodied.</param>
    /// <param name="block">The block body, when the member is block-bodied.</param>
    /// <returns><see langword="true"/> for an expression body of <c>true</c> or a block whose only result is <c>return true;</c>.</returns>
    private static bool IsAlwaysTrueBody(ExpressionSyntax? expressionBody, BlockSyntax? block)
    {
        if (expressionBody is not null)
        {
            return IsTrueLiteral(expressionBody);
        }

        return block is not null && BlockAlwaysReturnsTrue(block);
    }

    /// <summary>Returns whether every <c>return</c> in a block yields the literal <see langword="true"/>.</summary>
    /// <param name="block">The block body to inspect.</param>
    /// <returns><see langword="true"/> when the block has at least one return and all of them return <c>true</c>.</returns>
    private static bool BlockAlwaysReturnsTrue(BlockSyntax block)
    {
        var sawReturn = false;
        return AllReturnsTrue(block, ref sawReturn) && sawReturn;
    }

    /// <summary>Walks a body's own <c>return</c> statements, skipping nested functions.</summary>
    /// <param name="node">The current node whose children are scanned.</param>
    /// <param name="sawReturn">Set to <see langword="true"/> when any return belonging to this body is seen.</param>
    /// <returns><see langword="false"/> as soon as a return yields anything other than the literal <c>true</c>.</returns>
    private static bool AllReturnsTrue(SyntaxNode node, ref bool sawReturn)
    {
        foreach (var child in node.ChildNodes())
        {
            switch (child)
            {
                case ReturnStatementSyntax returnStatement:
                {
                    sawReturn = true;
                    if (!IsTrueLiteral(returnStatement.Expression))
                    {
                        return false;
                    }

                    break;
                }

                // A nested lambda, anonymous method, or local function owns its own returns; do not descend.
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                    break;

                default:
                {
                    if (!AllReturnsTrue(child, ref sawReturn))
                    {
                        return false;
                    }

                    break;
                }
            }
        }

        return true;
    }

    /// <summary>Returns whether an expression is the literal <see langword="true"/>, ignoring parentheses.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <returns><see langword="true"/> when the expression is a <c>true</c> literal.</returns>
    private static bool IsTrueLiteral(ExpressionSyntax? expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression?.IsKind(SyntaxKind.TrueLiteralExpression) == true;
    }
}
