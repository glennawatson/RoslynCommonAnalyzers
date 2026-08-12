// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Finds the cancellation token a call site is already holding: the nearest enclosing function
/// parameter declared as a <c>CancellationToken</c>, searching outward from the call through
/// lambdas and local functions to the containing method.
/// </summary>
/// <remarks>
/// The outward walk is purely syntactic so an invocation that has no token to pass costs nothing but
/// a short parent chain — no binding, no allocation. What the walk skips, it skips because passing it
/// would not compile: a <c>static</c> lambda or local function ends the walk after its own parameters
/// are checked, since nothing outside it can be captured, and a by-reference parameter is never taken
/// at all, since a nested function cannot capture one. Implicitly typed lambda parameters carry no
/// type to match against and are skipped, so the token behind <c>(item, token) =&gt; …</c> is only seen
/// when it is written with its type — the enclosing method's token is then used instead.
/// </remarks>
internal static class CancellationTokenScope
{
    /// <summary>The metadata name of the cancellation token type.</summary>
    public const string TokenMetadataName = "System.Threading.CancellationToken";

    /// <summary>The unqualified type name a parameter must be declared with to be considered.</summary>
    private const string TokenTypeName = "CancellationToken";

    /// <summary>Finds the nearest parameter in scope that is declared as a cancellation token.</summary>
    /// <param name="node">The node to search outward from.</param>
    /// <returns>The parameter, or <see langword="null"/> when no enclosing function declares one.</returns>
    public static ParameterSyntax? TryFindInScope(SyntaxNode node)
    {
        ParameterSyntax? found = null;
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is BaseMethodDeclarationSyntax method)
            {
                found = TryMatch(method.ParameterList.Parameters);
                break;
            }

            if (current is MemberDeclarationSyntax)
            {
                break;
            }

            if (TryGetNestedFunction(current) is not { } function)
            {
                continue;
            }

            found = TryMatch(function.Parameters);

            // A static function captures nothing, so an outer scope's token is out of reach from inside it.
            if (found is not null || ModifierListHelper.Contains(function.Modifiers, SyntaxKind.StaticKeyword))
            {
                break;
            }
        }

        return found;
    }

    /// <summary>Confirms a syntactically matched parameter really is a cancellation token, and names it.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="parameter">The parameter found in scope.</param>
    /// <param name="tokenType">The cancellation token type resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The parameter's name, or <see langword="null"/> when it binds to some other type.</returns>
    public static string? TryResolveName(
        SemanticModel model,
        ParameterSyntax parameter,
        INamedTypeSymbol tokenType,
        CancellationToken cancellationToken)
        => model.GetDeclaredSymbol(parameter, cancellationToken) is { } symbol
            && SymbolEqualityComparer.Default.Equals(symbol.Type, tokenType)
            ? symbol.Name
            : null;

    /// <summary>Finds the first parameter declared as a cancellation token and passable by value.</summary>
    /// <param name="parameters">The parameter list to scan.</param>
    /// <returns>The parameter, or <see langword="null"/> when the list declares none.</returns>
    private static ParameterSyntax? TryMatch(SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            if (parameter.Type is { } type && IsTokenTypeName(type) && IsPassableByValue(parameter.Modifiers))
            {
                return parameter;
            }
        }

        return null;
    }

    /// <summary>Returns whether a parameter can simply be handed to another call.</summary>
    /// <param name="modifiers">The parameter's modifiers.</param>
    /// <returns><see langword="false"/> for a by-reference parameter, which a nested function cannot capture.</returns>
    private static bool IsPassableByValue(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.RefKeyword)
                || modifiers[i].IsKind(SyntaxKind.OutKeyword)
                || modifiers[i].IsKind(SyntaxKind.InKeyword))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a type syntax names the cancellation token type.</summary>
    /// <param name="type">The declared type syntax.</param>
    /// <returns><see langword="true"/> for <c>CancellationToken</c> in either its bare or qualified form.</returns>
    private static bool IsTokenTypeName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == TokenTypeName,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == TokenTypeName,
        _ => false,
    };

    /// <summary>Reads the parameters and modifiers of a function nested inside a member.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns>The nested function, or <see langword="null"/> when the node is not one.</returns>
    private static NestedFunction? TryGetNestedFunction(SyntaxNode node) => node switch
    {
        ParenthesizedLambdaExpressionSyntax lambda => new NestedFunction(lambda.ParameterList.Parameters, lambda.Modifiers),
        SimpleLambdaExpressionSyntax simple => new NestedFunction(default, simple.Modifiers),
        AnonymousMethodExpressionSyntax anonymous => new NestedFunction(anonymous.ParameterList?.Parameters ?? default, anonymous.Modifiers),
        LocalFunctionStatementSyntax localFunction => new NestedFunction(localFunction.ParameterList.Parameters, localFunction.Modifiers),
        _ => null,
    };

    /// <summary>A lambda, anonymous method, or local function crossed on the way out of a call.</summary>
    /// <param name="Parameters">Its parameters, empty when it declares none or they are implicitly typed.</param>
    /// <param name="Modifiers">Its modifiers, which decide whether an outer scope is still capturable.</param>
    private readonly record struct NestedFunction(SeparatedSyntaxList<ParameterSyntax> Parameters, SyntaxTokenList Modifiers);
}
