// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>The list splits the entries-on-unique-lines code fixes apply, one per owning syntax node and list.</summary>
internal static class UniqueLineRewrites
{
    /// <summary>Builds the method-like declaration with each parameter moved to its own line.</summary>
    /// <param name="node">The declaration to rewrite.</param>
    /// <returns>The rewritten declaration, or the original when it has no parameter list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static BaseMethodDeclarationSyntax MethodParameters(BaseMethodDeclarationSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(
            node,
            static inner => inner.ParameterList,
            static (inner, list) => inner.WithParameterList(list));

    /// <summary>Builds the delegate declaration with each parameter moved to its own line.</summary>
    /// <param name="node">The delegate declaration to rewrite.</param>
    /// <returns>The rewritten declaration, or the original when it has no parameter list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static DelegateDeclarationSyntax DelegateParameters(DelegateDeclarationSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(
            node,
            static inner => inner.ParameterList,
            static (inner, list) => inner.WithParameterList(list));

    /// <summary>Builds the indexer declaration with each parameter moved to its own line.</summary>
    /// <param name="node">The indexer declaration to rewrite.</param>
    /// <returns>The rewritten declaration, or the original when its bracketed parameter list needs no change.</returns>
    /// <remarks>
    /// The line ending is resolved inside the rewrite rather than captured from around it: capturing it
    /// makes the lambda a closure, which allocates a display class on every rewrite. The node the
    /// rewrite receives is the one it would have been read from.
    /// </remarks>
    internal static IndexerDeclarationSyntax IndexerParameters(IndexerDeclarationSyntax node) =>
        node.ConvertNodeIfAble(
               static inner => inner.ParameterList?.Parameters,
               static (inner, parameters) => inner.WithParameterList(
                   SyntaxFactory.BracketedParameterList(
                       inner.ParameterList.OpenBracketToken
                           .WithTrailingTrivia(UniqueLineCodeFixerHelperExtensions.GetEndOfLine(inner, elastic: true)),
                       parameters,
                       SyntaxFactory.Token(SyntaxKind.CloseBracketToken))))
           ?? node;

    /// <summary>Builds the invocation expression with each argument moved to its own line.</summary>
    /// <param name="node">The invocation expression to rewrite.</param>
    /// <returns>The rewritten expression, or the original when it has no argument list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static InvocationExpressionSyntax InvocationArguments(InvocationExpressionSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitArgumentsOntoOwnLines(
            node,
            static inner => inner.ArgumentList,
            static (inner, list) => inner.WithArgumentList(list));

    /// <summary>Builds the object creation expression with each argument moved to its own line.</summary>
    /// <param name="node">The object creation expression to rewrite.</param>
    /// <returns>The rewritten expression, or the original when it has no argument list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ObjectCreationExpressionSyntax ObjectCreationArguments(ObjectCreationExpressionSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitArgumentsOntoOwnLines(
            node,
            static inner => inner.ArgumentList,
            static (inner, list) => inner.WithArgumentList(list));

    /// <summary>Builds the element access expression with each argument moved to its own line.</summary>
    /// <param name="node">The element access expression to rewrite.</param>
    /// <returns>The rewritten expression, or the original when its bracketed argument list needs no change.</returns>
    internal static ElementAccessExpressionSyntax ElementAccessArguments(ElementAccessExpressionSyntax node)
    {
        var endOfLine = UniqueLineCodeFixerHelperExtensions.GetEndOfLine(node, elastic: true);
        return node.ConvertNodeIfAble(
                   static inner => inner.ArgumentList?.Arguments,
                   (inner, arguments) => inner.WithArgumentList(
                       SyntaxFactory.BracketedArgumentList(
                           inner.ArgumentList.OpenBracketToken.WithTrailingTrivia(endOfLine),
                           arguments,
                           SyntaxFactory.Token(SyntaxKind.CloseBracketToken))))
               ?? node;
    }

    /// <summary>Builds the attribute with each argument moved to its own line.</summary>
    /// <param name="node">The attribute to rewrite.</param>
    /// <returns>The rewritten attribute, or the original when its argument list needs no change.</returns>
    internal static AttributeSyntax AttributeArguments(AttributeSyntax node)
    {
        var endOfLine = UniqueLineCodeFixerHelperExtensions.GetEndOfLine(node, elastic: true);
        return node.ConvertNodeIfAble(
                   static inner => inner.ArgumentList?.Arguments,
                   (inner, arguments) => inner.WithArgumentList(
                       SyntaxFactory.AttributeArgumentList(
                           inner.ArgumentList!.OpenParenToken.WithTrailingTrivia(endOfLine),
                           arguments,
                           SyntaxFactory.Token(SyntaxKind.CloseParenToken))))
               ?? node;
    }

    /// <summary>Builds the anonymous method expression with each parameter moved to its own line.</summary>
    /// <param name="node">The anonymous method expression to rewrite.</param>
    /// <returns>The rewritten expression, or the original when it has no parameter list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static AnonymousMethodExpressionSyntax AnonymousMethodParameters(AnonymousMethodExpressionSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(
            node,
            static inner => inner.ParameterList,
            static (inner, list) => inner.WithParameterList(list));

    /// <summary>Builds the parenthesized lambda expression with each parameter moved to its own line.</summary>
    /// <param name="node">The parenthesized lambda expression to rewrite.</param>
    /// <returns>The rewritten expression, or the original when it has no parameter list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ParenthesizedLambdaExpressionSyntax LambdaParameters(ParenthesizedLambdaExpressionSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(
            node,
            static inner => inner.ParameterList,
            static (inner, list) => inner.WithParameterList(list));

    /// <summary>Builds the record declaration with each parameter moved to its own line.</summary>
    /// <param name="node">The record declaration to rewrite.</param>
    /// <returns>The rewritten declaration, or the original when it has no parameter list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static RecordDeclarationSyntax RecordParameters(RecordDeclarationSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(
            node,
            static inner => inner.ParameterList,
            static (inner, list) => inner.WithParameterList(list));

    /// <summary>Builds the class declaration with each primary constructor parameter moved to its own line.</summary>
    /// <param name="node">The class declaration to rewrite.</param>
    /// <returns>The rewritten declaration, or the original when it has no parameter list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ClassDeclarationSyntax ClassParameters(ClassDeclarationSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(
            node,
            static inner => inner.ParameterList,
            static (inner, list) => inner.WithParameterList(list));

    /// <summary>Builds the struct declaration with each primary constructor parameter moved to its own line.</summary>
    /// <param name="node">The struct declaration to rewrite.</param>
    /// <returns>The rewritten declaration, or the original when it has no parameter list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static StructDeclarationSyntax StructParameters(StructDeclarationSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(
            node,
            static inner => inner.ParameterList,
            static (inner, list) => inner.WithParameterList(list));

    /// <summary>Builds the implicit object creation expression with each argument moved to its own line.</summary>
    /// <param name="node">The implicit object creation expression to rewrite.</param>
    /// <returns>The rewritten expression, or the original when it has no argument list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ImplicitObjectCreationExpressionSyntax ImplicitObjectCreationArguments(ImplicitObjectCreationExpressionSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitArgumentsOntoOwnLines(
            node,
            static inner => inner.ArgumentList,
            static (inner, list) => inner.WithArgumentList(list));

    /// <summary>Builds the constructor initializer with each argument moved to its own line.</summary>
    /// <param name="node">The constructor initializer to rewrite.</param>
    /// <returns>The rewritten initializer, or the original when it has no argument list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ConstructorInitializerSyntax ConstructorInitializerArguments(ConstructorInitializerSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitArgumentsOntoOwnLines(
            node,
            static inner => inner.ArgumentList,
            static (inner, list) => inner.WithArgumentList(list));

    /// <summary>Builds the primary constructor base type with each argument moved to its own line.</summary>
    /// <param name="node">The primary constructor base type to rewrite.</param>
    /// <returns>The rewritten base type, or the original when it has no argument list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PrimaryConstructorBaseTypeSyntax PrimaryConstructorBaseTypeArguments(PrimaryConstructorBaseTypeSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitArgumentsOntoOwnLines(
            node,
            static inner => inner.ArgumentList,
            static (inner, list) => inner.WithArgumentList(list));

    /// <summary>Builds the local function statement with each parameter moved to its own line.</summary>
    /// <param name="node">The local function statement to rewrite.</param>
    /// <returns>The rewritten statement, or the original when it has no parameter list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static LocalFunctionStatementSyntax LocalFunctionParameters(LocalFunctionStatementSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(
            node,
            static inner => inner.ParameterList,
            static (inner, list) => inner.WithParameterList(list));

    /// <summary>Builds the operator declaration with each parameter moved to its own line.</summary>
    /// <param name="node">The operator declaration to rewrite.</param>
    /// <returns>The rewritten declaration, or the original when it has no parameter list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OperatorDeclarationSyntax OperatorParameters(OperatorDeclarationSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(
            node,
            static inner => inner.ParameterList,
            static (inner, list) => inner.WithParameterList(list));

    /// <summary>Builds the conversion operator declaration with each parameter moved to its own line.</summary>
    /// <param name="node">The conversion operator declaration to rewrite.</param>
    /// <returns>The rewritten declaration, or the original when it has no parameter list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ConversionOperatorDeclarationSyntax ConversionOperatorParameters(ConversionOperatorDeclarationSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(
            node,
            static inner => inner.ParameterList,
            static (inner, list) => inner.WithParameterList(list));

    /// <summary>Builds the type parameter list with each type parameter moved to its own line.</summary>
    /// <param name="node">The type parameter list to rewrite.</param>
    /// <returns>The rewritten list, or the original when it needs no change.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TypeParameterListSyntax TypeParameters(TypeParameterListSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitAngleBracketedListOntoOwnLines(
            node,
            node.Parameters,
            (list, endOfLine) => SyntaxFactory.TypeParameterList(node.LessThanToken.WithTrailingTrivia(endOfLine), list, node.GreaterThanToken));

    /// <summary>Builds the type argument list with each type argument moved to its own line.</summary>
    /// <param name="node">The type argument list to rewrite.</param>
    /// <returns>The rewritten list, or the original when it needs no change.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TypeArgumentListSyntax TypeArguments(TypeArgumentListSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitAngleBracketedListOntoOwnLines(
            node,
            node.Arguments,
            (list, endOfLine) => SyntaxFactory.TypeArgumentList(node.LessThanToken.WithTrailingTrivia(endOfLine), list, node.GreaterThanToken));

    /// <summary>Builds the function pointer parameter list with each parameter moved to its own line.</summary>
    /// <param name="node">The function pointer parameter list to rewrite.</param>
    /// <returns>The rewritten list, or the original when it needs no change.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FunctionPointerParameterListSyntax FunctionPointerParameters(FunctionPointerParameterListSyntax node) =>
        UniqueLineCodeFixerHelperExtensions.SplitAngleBracketedListOntoOwnLines(
            node,
            node.Parameters,
            (list, endOfLine) => SyntaxFactory.FunctionPointerParameterList(node.LessThanToken.WithTrailingTrivia(endOfLine), list, node.GreaterThanToken));
}
