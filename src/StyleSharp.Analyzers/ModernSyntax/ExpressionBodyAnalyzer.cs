// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(
        ModernSyntaxRules.UseExpressionBodyForMethod,
        ModernSyntaxRules.UseExpressionBodyForConstructor,
        ModernSyntaxRules.UseExpressionBodyForOperator,
        ModernSyntaxRules.UseExpressionBodyForConversionOperator,
        ModernSyntaxRules.UseExpressionBodyForProperty,
        ModernSyntaxRules.UseExpressionBodyForIndexer,
        ModernSyntaxRules.UseExpressionBodyForLocalFunction);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.OperatorDeclaration,
            SyntaxKind.ConversionOperatorDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.IndexerDeclaration,
            SyntaxKind.LocalFunctionStatement);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool AccessorListCollapsesToExpressionBody(AccessorListSyntax? accessorList) =>
        TryGetSoleGetAccessorExpression(accessorList, out _);

    /// <summary>Reports a member whose block body can collapse to an expression body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        switch (context.Node)
        {
            case MethodDeclarationSyntax method when SupportsExpressionBody(method, LanguageVersion.CSharp6) && TryGetMethodExpression(method, out _):
                {
                    Report(context, ModernSyntaxRules.UseExpressionBodyForMethod, method.Identifier);
                    break;
                }

            case ConstructorDeclarationSyntax constructor when SupportsExpressionBody(constructor, LanguageVersion.CSharp7) && TryGetConstructorExpression(constructor, out _):
                {
                    Report(context, ModernSyntaxRules.UseExpressionBodyForConstructor, constructor.Identifier);
                    break;
                }

            case OperatorDeclarationSyntax operatorDeclaration when SupportsExpressionBody(operatorDeclaration, LanguageVersion.CSharp6) && TryGetOperatorExpression(operatorDeclaration, out _):
                {
                    Report(context, ModernSyntaxRules.UseExpressionBodyForOperator, operatorDeclaration.OperatorToken);
                    break;
                }

            case ConversionOperatorDeclarationSyntax conversion when SupportsExpressionBody(conversion, LanguageVersion.CSharp6) && TryGetConversionOperatorExpression(conversion, out _):
                {
                    Report(context, ModernSyntaxRules.UseExpressionBodyForConversionOperator, conversion.OperatorKeyword);
                    break;
                }

            case PropertyDeclarationSyntax property when SupportsExpressionBody(property, LanguageVersion.CSharp6) && TryGetPropertyExpression(property, out _):
                {
                    Report(context, ModernSyntaxRules.UseExpressionBodyForProperty, property.Identifier);
                    break;
                }

            case IndexerDeclarationSyntax indexer when SupportsExpressionBody(indexer, LanguageVersion.CSharp6) && TryGetIndexerExpression(indexer, out _):
                {
                    Report(context, ModernSyntaxRules.UseExpressionBodyForIndexer, indexer.ThisKeyword);
                    break;
                }

            case LocalFunctionStatementSyntax localFunction when SupportsExpressionBody(localFunction, LanguageVersion.CSharp7) && TryGetLocalFunctionExpression(localFunction, out _):
                {
                    Report(context, ModernSyntaxRules.UseExpressionBodyForLocalFunction, localFunction.Identifier);
                    break;
                }
        }
    }

    /// <summary>Reports a rule at the token that names the member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="rule">The rule for the member's kind.</param>
    /// <param name="token">The token that names the member.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Report(in SyntaxNodeAnalysisContext context, DiagnosticDescriptor rule, SyntaxToken token) =>
        context.ReportDiagnostic(DiagnosticHelper.Create(rule, token.GetLocation()));

    /// <summary>Gets the single value returned or evaluated by a block body.</summary>
    /// <param name="body">The block body.</param>
    /// <param name="expression">The single expression.</param>
    /// <returns><see langword="true"/> when the block is one <c>return expr;</c> or one expression statement.</returns>
    private static bool TryGetReturnedOrEvaluated(BlockSyntax? body, out ExpressionSyntax expression) =>
        TryGetReturned(body, out expression) || TryGetEvaluated(body, out expression);

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
    /// The expression's own <see cref="SyntaxNode.Span"/> is the measure, not its full span: the fix splices the
    /// expression in with its leading and trailing trivia removed, so a comment sitting in either — such as one
    /// on the line above the statement — is lost even though the full span covers it. Trivia between the
    /// expression's own tokens survives and is inside the span, so it is correctly not counted.
    /// </remarks>
    private static bool WouldDropComment(SyntaxNode container, ExpressionSyntax expression)
    {
        if (InactivePreprocessorRegions.Contains(container))
        {
            return true;
        }

        var state = new CommentLossScan(expression.Span, container.Span.End);
        return !DescendantTraversalHelper.VisitDescendantTokens(
            container,
            ref state,
            static (in token, ref current) => !DropsComment(token.LeadingTrivia, current) && !DropsComment(token.TrailingTrivia, current));
    }

    /// <summary>Returns whether a trivia list holds a comment the collapse would drop.</summary>
    /// <param name="triviaList">The leading or trailing trivia of one token.</param>
    /// <param name="scan">The kept expression's span and the container's end.</param>
    /// <returns><see langword="true"/> when a comment sits inside the container but outside the kept expression.</returns>
    private static bool DropsComment(in SyntaxTriviaList triviaList, in CommentLossScan scan)
    {
        foreach (var trivia in triviaList)
        {
            if (IsComment(trivia.Kind())
                && !scan.ExpressionSpan.Contains(trivia.SpanStart)
                && trivia.SpanStart < scan.ContainerEnd)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a trivia kind is a comment that carries meaning worth preserving.</summary>
    /// <param name="kind">The trivia kind.</param>
    /// <returns><see langword="true"/> for single-line, multi-line, and documentation comments.</returns>
    private static bool IsComment(SyntaxKind kind) =>
        kind is SyntaxKind.SingleLineCommentTrivia
            or SyntaxKind.MultiLineCommentTrivia
            or SyntaxKind.SingleLineDocumentationCommentTrivia
            or SyntaxKind.MultiLineDocumentationCommentTrivia;

    /// <summary>Returns whether the node's language version supports the expression-bodied member form.</summary>
    /// <param name="node">A node in the tree under analysis.</param>
    /// <param name="minimum">The minimum language version that allows the member kind's expression body.</param>
    /// <returns><see langword="true"/> when the tree parses at or above <paramref name="minimum"/>.</returns>
    private static bool SupportsExpressionBody(SyntaxNode node, LanguageVersion minimum) =>
        node.SyntaxTree.Options is CSharpParseOptions { } options && options.LanguageVersion >= minimum;
}
