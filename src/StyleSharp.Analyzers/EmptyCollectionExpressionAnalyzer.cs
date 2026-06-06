// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Suggests <c>[]</c> for empty standard collection creations (SST2100).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyCollectionExpressionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CollectionExpressionRules.UseEmptyCollectionExpression);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(start =>
        {
            var targets = CollectionExpressionHelper.ResolveTargets(start.Compilation);
            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, targets),
                SyntaxKind.InvocationExpression,
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ArrayCreationExpression);
        });
    }

    /// <summary>Reports an accepted empty collection creation.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="targets">The accepted target definitions.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol[] targets)
    {
        if (context.Node is not ExpressionSyntax expression
            || !CollectionExpressionHelper.IsLanguageSupported(expression)
            || !IsEmptyCandidate(expression)
            || !CollectionExpressionHelper.HasAcceptedTarget(context, expression, targets))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(CollectionExpressionRules.UseEmptyCollectionExpression, expression.GetLocation()));
    }

    /// <summary>Returns whether an expression is syntactically an empty collection creation.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns><see langword="true"/> for an empty candidate.</returns>
    private static bool IsEmptyCandidate(ExpressionSyntax expression) => expression switch
    {
        InvocationExpressionSyntax invocation => IsEmptyInvocation(invocation),
        ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0, Initializer: null } => true,
        ArrayCreationExpressionSyntax array => IsEmptyArray(array),
        _ => false,
    };

    /// <summary>Returns whether an invocation is <c>Array.Empty</c> or <c>Enumerable.Empty</c>.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns><see langword="true"/> for a supported empty factory.</returns>
    private static bool IsEmptyInvocation(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.Text: "Empty", TypeArgumentList.Arguments.Count: 1 } } access)
        {
            return false;
        }

        return access.Expression is IdentifierNameSyntax { Identifier.Text: "Array" or "Enumerable" }
            or MemberAccessExpressionSyntax { Name.Identifier.Text: "Array" or "Enumerable" };
    }

    /// <summary>Returns whether an array creation is empty.</summary>
    /// <param name="array">The array creation.</param>
    /// <returns><see langword="true"/> for an empty initializer or zero length.</returns>
    private static bool IsEmptyArray(ArrayCreationExpressionSyntax array)
    {
        if (array.Initializer is { Expressions.Count: 0 })
        {
            return true;
        }

        return array.Type.RankSpecifiers is [{ Rank: 1, Sizes: [LiteralExpressionSyntax literal] }]
            && literal.Token.ValueText.AsSpan().SequenceEqual("0".AsSpan());
    }
}
