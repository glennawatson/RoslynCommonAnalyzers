// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>IndexOfAny</c>-family calls whose set argument is an inline constant char array
/// or collection expression (PSH1213). Every call rescans the set linearly and the inline
/// creation can allocate each time; a <c>static readonly SearchValues&lt;char&gt;</c>
/// (.NET 8+) precomputes the membership table once and the overloads that take it probe in
/// constant time per character. Reported without a fix — the rewrite hoists a static field
/// the surrounding type must adopt — and only when the call binds to the string or span
/// search APIs and <c>SearchValues</c> exists in the compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1213UseSearchValuesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the search values factory the rule is gated on.</summary>
    private const string SearchValuesMetadataName = "System.Buffers.SearchValues";

    /// <summary>The metadata name of the span extensions type.</summary>
    private const string MemoryExtensionsMetadataName = "System.MemoryExtensions";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseSearchValues);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(SearchValuesMetadataName) is null
                || start.Compilation.GetTypeByMetadataName(MemoryExtensionsMetadataName) is not { } extensions)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, extensions),
                SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether a member name is one of the any-of search methods.</summary>
    /// <param name="name">The invoked member name.</param>
    /// <returns><see langword="true"/> for the IndexOfAny family.</returns>
    private static bool IsAnyOfSearchName(string name)
        => name is "IndexOfAny" or "LastIndexOfAny" or "IndexOfAnyExcept" or "LastIndexOfAnyExcept"
            or "ContainsAny" or "ContainsAnyExcept";

    /// <summary>Returns whether an expression is an inline creation of constant chars.</summary>
    /// <param name="expression">The set argument.</param>
    /// <returns><see langword="true"/> for array creations and collection expressions of char literals.</returns>
    private static bool IsInlineConstantCharSet(ExpressionSyntax expression)
        => expression switch
        {
            ArrayCreationExpressionSyntax { Initializer: { } initializer } => AllCharLiterals(initializer.Expressions),
            ImplicitArrayCreationExpressionSyntax { Initializer: { } initializer } => AllCharLiterals(initializer.Expressions),
            CollectionExpressionSyntax collection => AllCharLiteralElements(collection),
            _ => false,
        };

    /// <summary>Returns whether every expression in a list is a char literal.</summary>
    /// <param name="expressions">The initializer expressions.</param>
    /// <returns><see langword="true"/> when the set is wholly constant.</returns>
    private static bool AllCharLiterals(SeparatedSyntaxList<ExpressionSyntax> expressions)
    {
        if (expressions.Count == 0)
        {
            return false;
        }

        foreach (var expression in expressions)
        {
            if (!expression.IsKind(SyntaxKind.CharacterLiteralExpression))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether every element of a collection expression is a char literal.</summary>
    /// <param name="collection">The collection expression.</param>
    /// <returns><see langword="true"/> when the set is wholly constant.</returns>
    private static bool AllCharLiteralElements(CollectionExpressionSyntax collection)
    {
        if (collection.Elements.Count == 0)
        {
            return false;
        }

        foreach (var element in collection.Elements)
        {
            if (element is not ExpressionElementSyntax expressionElement
                || !expressionElement.Expression.IsKind(SyntaxKind.CharacterLiteralExpression))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reports PSH1213 for an any-of search over an inline constant set.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="extensions">The span extensions type.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol extensions)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 1
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || !IsAnyOfSearchName(access.Name.Identifier.ValueText)
            || !IsInlineConstantCharSet(invocation.ArgumentList.Arguments[0].Expression))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsSearchApi(method, extensions))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseSearchValues,
            invocation.SyntaxTree,
            invocation.Span));
    }

    /// <summary>Returns whether a bound method is one of the runtime's search APIs.</summary>
    /// <param name="method">The bound method.</param>
    /// <param name="extensions">The span extensions type.</param>
    /// <returns><see langword="true"/> for string members and MemoryExtensions extensions.</returns>
    private static bool IsSearchApi(IMethodSymbol method, INamedTypeSymbol extensions)
    {
        var containingType = (method.ReducedFrom ?? method).ContainingType;
        return containingType.SpecialType == SpecialType.System_String
            || SymbolEqualityComparer.Default.Equals(containingType, extensions);
    }
}
