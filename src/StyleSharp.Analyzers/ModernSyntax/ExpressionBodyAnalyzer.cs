// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a member whose block body is a single expression-shaped statement and can use an
/// expression body <c>=&gt; expr</c>. Each member kind carries its own id: a method (SST2275),
/// a constructor (SST2276), an operator (SST2277), a conversion operator (SST2278), a get-only
/// property (SST2279), a get-only indexer (SST2280), or a local function (SST2281). Accessor
/// bodies and lambda bodies are covered by their own rules and are not reported here.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExpressionBodyAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ModernSyntaxRules.UseExpressionBodyForMethod,
        ModernSyntaxRules.UseExpressionBodyForConstructor,
        ModernSyntaxRules.UseExpressionBodyForOperator,
        ModernSyntaxRules.UseExpressionBodyForConversionOperator,
        ModernSyntaxRules.UseExpressionBodyForProperty,
        ModernSyntaxRules.UseExpressionBodyForIndexer,
        ModernSyntaxRules.UseExpressionBodyForLocalFunction);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeOperator, SyntaxKind.OperatorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeConversionOperator, SyntaxKind.ConversionOperatorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeIndexer, SyntaxKind.IndexerDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeLocalFunction, SyntaxKind.LocalFunctionStatement);
    }

    /// <summary>Gets the single expression a method's block body can collapse to.</summary>
    /// <param name="method">The method declaration.</param>
    /// <param name="expression">The single returned or evaluated expression.</param>
    /// <returns><see langword="true"/> when the block body is one <c>return expr;</c> or one expression statement.</returns>
    internal static bool TryGetMethodExpression(MethodDeclarationSyntax method, out ExpressionSyntax expression)
    {
        expression = null!;
        return method.ExpressionBody is null
            && TryGetReturnedOrEvaluated(method.Body, out expression)
            && !WouldDropComment(method.Body!, expression);
    }

    /// <summary>Gets the single expression a constructor's block body can collapse to.</summary>
    /// <param name="constructor">The constructor declaration.</param>
    /// <param name="expression">The single evaluated expression.</param>
    /// <returns><see langword="true"/> when the constructor has no initializer and one expression-statement body.</returns>
    internal static bool TryGetConstructorExpression(ConstructorDeclarationSyntax constructor, out ExpressionSyntax expression)
    {
        expression = null!;
        return constructor.ExpressionBody is null
            && constructor.Initializer is null
            && TryGetEvaluated(constructor.Body, out expression)
            && !WouldDropComment(constructor.Body!, expression);
    }

    /// <summary>Gets the single expression an operator's block body can collapse to.</summary>
    /// <param name="operatorDeclaration">The operator declaration.</param>
    /// <param name="expression">The single returned expression.</param>
    /// <returns><see langword="true"/> when the block body is one <c>return expr;</c>.</returns>
    internal static bool TryGetOperatorExpression(OperatorDeclarationSyntax operatorDeclaration, out ExpressionSyntax expression)
    {
        expression = null!;
        return operatorDeclaration.ExpressionBody is null
            && TryGetReturned(operatorDeclaration.Body, out expression)
            && !WouldDropComment(operatorDeclaration.Body!, expression);
    }

    /// <summary>Gets the single expression a conversion operator's block body can collapse to.</summary>
    /// <param name="conversion">The conversion operator declaration.</param>
    /// <param name="expression">The single returned expression.</param>
    /// <returns><see langword="true"/> when the block body is one <c>return expr;</c>.</returns>
    internal static bool TryGetConversionOperatorExpression(ConversionOperatorDeclarationSyntax conversion, out ExpressionSyntax expression)
    {
        expression = null!;
        return conversion.ExpressionBody is null
            && TryGetReturned(conversion.Body, out expression)
            && !WouldDropComment(conversion.Body!, expression);
    }

    /// <summary>Gets the single expression a get-only property can collapse to.</summary>
    /// <param name="property">The property declaration.</param>
    /// <param name="expression">The single returned expression.</param>
    /// <returns><see langword="true"/> when the property has a single block-bodied <c>get</c> that returns one value.</returns>
    internal static bool TryGetPropertyExpression(PropertyDeclarationSyntax property, out ExpressionSyntax expression)
    {
        expression = null!;
        return property.ExpressionBody is null
            && TryGetSoleGetAccessorExpression(property.AccessorList, out expression);
    }

    /// <summary>Gets the single expression a get-only indexer can collapse to.</summary>
    /// <param name="indexer">The indexer declaration.</param>
    /// <param name="expression">The single returned expression.</param>
    /// <returns><see langword="true"/> when the indexer has a single block-bodied <c>get</c> that returns one value.</returns>
    internal static bool TryGetIndexerExpression(IndexerDeclarationSyntax indexer, out ExpressionSyntax expression)
    {
        expression = null!;
        return indexer.ExpressionBody is null
            && TryGetSoleGetAccessorExpression(indexer.AccessorList, out expression);
    }

    /// <summary>Gets the single expression a local function's block body can collapse to.</summary>
    /// <param name="localFunction">The local function statement.</param>
    /// <param name="expression">The single returned or evaluated expression.</param>
    /// <returns><see langword="true"/> when the block body is one <c>return expr;</c> or one expression statement.</returns>
    internal static bool TryGetLocalFunctionExpression(LocalFunctionStatementSyntax localFunction, out ExpressionSyntax expression)
    {
        expression = null!;
        return localFunction.ExpressionBody is null
            && TryGetReturnedOrEvaluated(localFunction.Body, out expression)
            && !WouldDropComment(localFunction.Body!, expression);
    }

    /// <summary>Returns whether a property or indexer accessor list collapses to a whole-member expression body.</summary>
    /// <param name="accessorList">The accessor list of a property or indexer.</param>
    /// <returns><see langword="true"/> when the list is a single block-bodied <c>get</c> that SST2279 or SST2280 reports.</returns>
    internal static bool AccessorListCollapsesToExpressionBody(AccessorListSyntax? accessorList)
        => TryGetSoleGetAccessorExpression(accessorList, out _);

    /// <summary>Reports a single-statement method that can use an expression body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        if (!SupportsExpressionBody(context.Node, LanguageVersion.CSharp6))
        {
            return;
        }

        var method = (MethodDeclarationSyntax)context.Node;
        if (!TryGetMethodExpression(method, out _))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseExpressionBodyForMethod, method.Identifier.GetLocation()));
    }

    /// <summary>Reports a single-call constructor that can use an expression body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        if (!SupportsExpressionBody(context.Node, LanguageVersion.CSharp7))
        {
            return;
        }

        var constructor = (ConstructorDeclarationSyntax)context.Node;
        if (!TryGetConstructorExpression(constructor, out _))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseExpressionBodyForConstructor, constructor.Identifier.GetLocation()));
    }

    /// <summary>Reports a single-return operator that can use an expression body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeOperator(SyntaxNodeAnalysisContext context)
    {
        if (!SupportsExpressionBody(context.Node, LanguageVersion.CSharp6))
        {
            return;
        }

        var operatorDeclaration = (OperatorDeclarationSyntax)context.Node;
        if (!TryGetOperatorExpression(operatorDeclaration, out _))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseExpressionBodyForOperator, operatorDeclaration.OperatorToken.GetLocation()));
    }

    /// <summary>Reports a single-return conversion operator that can use an expression body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeConversionOperator(SyntaxNodeAnalysisContext context)
    {
        if (!SupportsExpressionBody(context.Node, LanguageVersion.CSharp6))
        {
            return;
        }

        var conversion = (ConversionOperatorDeclarationSyntax)context.Node;
        if (!TryGetConversionOperatorExpression(conversion, out _))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseExpressionBodyForConversionOperator, conversion.OperatorKeyword.GetLocation()));
    }

    /// <summary>Reports a get-only property that can use a whole-member expression body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        if (!SupportsExpressionBody(context.Node, LanguageVersion.CSharp6))
        {
            return;
        }

        var property = (PropertyDeclarationSyntax)context.Node;
        if (!TryGetPropertyExpression(property, out _))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseExpressionBodyForProperty, property.Identifier.GetLocation()));
    }

    /// <summary>Reports a get-only indexer that can use a whole-member expression body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeIndexer(SyntaxNodeAnalysisContext context)
    {
        if (!SupportsExpressionBody(context.Node, LanguageVersion.CSharp6))
        {
            return;
        }

        var indexer = (IndexerDeclarationSyntax)context.Node;
        if (!TryGetIndexerExpression(indexer, out _))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseExpressionBodyForIndexer, indexer.ThisKeyword.GetLocation()));
    }

    /// <summary>Reports a single-statement local function that can use an expression body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeLocalFunction(SyntaxNodeAnalysisContext context)
    {
        if (!SupportsExpressionBody(context.Node, LanguageVersion.CSharp7))
        {
            return;
        }

        var localFunction = (LocalFunctionStatementSyntax)context.Node;
        if (!TryGetLocalFunctionExpression(localFunction, out _))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseExpressionBodyForLocalFunction, localFunction.Identifier.GetLocation()));
    }

    /// <summary>Gets the single value returned or evaluated by a block body.</summary>
    /// <param name="body">The block body.</param>
    /// <param name="expression">The single expression.</param>
    /// <returns><see langword="true"/> when the block is one <c>return expr;</c> or one expression statement.</returns>
    private static bool TryGetReturnedOrEvaluated(BlockSyntax? body, out ExpressionSyntax expression)
        => TryGetReturned(body, out expression) || TryGetEvaluated(body, out expression);

    /// <summary>Gets the single value a block body returns.</summary>
    /// <param name="body">The block body.</param>
    /// <param name="expression">The returned expression.</param>
    /// <returns><see langword="true"/> when the block is a single <c>return expr;</c>.</returns>
    private static bool TryGetReturned(BlockSyntax? body, out ExpressionSyntax expression)
    {
        if (body is { Statements.Count: 1 } block
            && block.Statements[0] is ReturnStatementSyntax { Expression: { } returned })
        {
            expression = returned;
            return true;
        }

        expression = null!;
        return false;
    }

    /// <summary>Gets the single value a block body evaluates as a statement.</summary>
    /// <param name="body">The block body.</param>
    /// <param name="expression">The evaluated expression.</param>
    /// <returns><see langword="true"/> when the block is a single expression statement.</returns>
    private static bool TryGetEvaluated(BlockSyntax? body, out ExpressionSyntax expression)
    {
        if (body is { Statements.Count: 1 } block
            && block.Statements[0] is ExpressionStatementSyntax { Expression: { } evaluated })
        {
            expression = evaluated;
            return true;
        }

        expression = null!;
        return false;
    }

    /// <summary>Gets the single returned expression of a property or indexer that has one block-bodied <c>get</c>.</summary>
    /// <param name="accessorList">The accessor list.</param>
    /// <param name="expression">The single returned expression.</param>
    /// <returns><see langword="true"/> when the sole accessor is a plain block-bodied <c>get</c> returning one value.</returns>
    private static bool TryGetSoleGetAccessorExpression(AccessorListSyntax? accessorList, out ExpressionSyntax expression)
    {
        expression = null!;
        if (accessorList is not { Accessors.Count: 1 } list)
        {
            return false;
        }

        var accessor = list.Accessors[0];
        return accessor.IsKind(SyntaxKind.GetAccessorDeclaration)
            && accessor.ExpressionBody is null
            && accessor.AttributeLists.Count == 0
            && accessor.Modifiers.Count == 0
            && TryGetReturned(accessor.Body, out expression)
            && !WouldDropComment(list, expression);
    }

    /// <summary>Returns whether collapsing a block to an expression body would drop a comment.</summary>
    /// <param name="container">The block or accessor list being collapsed.</param>
    /// <param name="expression">The expression that survives the collapse.</param>
    /// <returns><see langword="true"/> when a comment inside the container, other than the kept expression, would be lost.</returns>
    /// <remarks>
    /// Runs only after the single-statement shape has matched, so the clean path never reaches it. The trailing
    /// trivia after the container is carried onto the new semicolon, so a comment there is not counted as dropped.
    /// </remarks>
    private static bool WouldDropComment(SyntaxNode container, ExpressionSyntax expression)
    {
        var expressionSpan = expression.FullSpan;
        var containerEnd = container.Span.End;
        foreach (var trivia in container.DescendantTrivia())
        {
            if (!IsComment(trivia.Kind())
                || expressionSpan.Contains(trivia.SpanStart)
                || trivia.SpanStart >= containerEnd)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>Returns whether a trivia kind is a comment that carries meaning worth preserving.</summary>
    /// <param name="kind">The trivia kind.</param>
    /// <returns><see langword="true"/> for single-line, multi-line, and documentation comments.</returns>
    private static bool IsComment(SyntaxKind kind)
        => kind is SyntaxKind.SingleLineCommentTrivia
            or SyntaxKind.MultiLineCommentTrivia
            or SyntaxKind.SingleLineDocumentationCommentTrivia
            or SyntaxKind.MultiLineDocumentationCommentTrivia;

    /// <summary>Returns whether the node's language version supports the expression-bodied member form.</summary>
    /// <param name="node">A node in the tree under analysis.</param>
    /// <param name="minimum">The minimum language version that allows the member kind's expression body.</param>
    /// <returns><see langword="true"/> when the tree parses at or above <paramref name="minimum"/>.</returns>
    private static bool SupportsExpressionBody(SyntaxNode node, LanguageVersion minimum)
        => node.SyntaxTree.Options is CSharpParseOptions { } options && options.LanguageVersion >= minimum;
}
