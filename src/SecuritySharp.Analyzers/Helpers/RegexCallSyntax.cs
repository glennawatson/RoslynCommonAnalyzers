// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Recognizes a <c>new Regex(pattern, ...)</c> construction and a static <c>Regex.Method(input, pattern, ...)</c>
/// call, shared by the rules that inspect a regex pattern argument (SES1303, SES1509): free syntax gates first,
/// then binding checks against the resolved <c>Regex</c> type.
/// </summary>
internal static class RegexCallSyntax
{
    /// <summary>The simple type name the construction gate accepts.</summary>
    private const string RegexTypeName = "Regex";

    /// <summary>The fewest arguments a static pattern call passes: the input and the pattern.</summary>
    private const int StaticPatternCallMinimumArguments = 2;

    /// <summary>Returns the argument list of a construction written as <c>new Regex(...)</c> with at least one argument, before any binding.</summary>
    /// <param name="creation">The object creation.</param>
    /// <param name="argumentList">The construction's arguments, when the shape matches.</param>
    /// <returns><see langword="true"/> when the created type is written as <c>Regex</c>, simple or qualified.</returns>
    internal static bool TryGetCreationArguments(ObjectCreationExpressionSyntax creation, [NotNullWhen(true)] out ArgumentListSyntax? argumentList)
    {
        if (creation.ArgumentList is { Arguments.Count: > 0 } arguments
            && creation.Type is IdentifierNameSyntax { Identifier.ValueText: RegexTypeName } or QualifiedNameSyntax { Right.Identifier.ValueText: RegexTypeName })
        {
            argumentList = arguments;
            return true;
        }

        argumentList = null;
        return false;
    }

    /// <summary>Returns the member name of a <c>receiver.Method(input, pattern, ...)</c> call, before any binding.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="methodName">The invoked member name, when the shape matches.</param>
    /// <returns><see langword="true"/> for a member call carrying at least the input and pattern arguments.</returns>
    internal static bool TryGetStaticCallName(InvocationExpressionSyntax invocation, [NotNullWhen(true)] out string? methodName)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax access
            && invocation.ArgumentList.Arguments.Count >= StaticPatternCallMinimumArguments)
        {
            methodName = access.Name.Identifier.ValueText;
            return true;
        }

        methodName = null;
        return false;
    }

    /// <summary>Returns whether a name is one of the static <c>Regex</c> methods taking <c>(input, pattern, ...)</c> that every pattern rule guards.</summary>
    /// <param name="name">The candidate method name.</param>
    /// <returns><see langword="true"/> for <c>IsMatch</c>, <c>Match</c>, <c>Matches</c>, <c>Replace</c>, or <c>Split</c>.</returns>
    internal static bool IsPatternMethodName(string name) =>
        name switch
        {
            "IsMatch" or "Match" or "Matches" or "Replace" or "Split" => true,
            _ => false,
        };

    /// <summary>Returns whether a construction binds to a constructor of the resolved <c>Regex</c> type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="creation">The object creation.</param>
    /// <param name="regexType">The compilation's <c>Regex</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="constructor">The bound constructor, when it belongs to <paramref name="regexType"/>.</param>
    /// <returns><see langword="true"/> for a <c>Regex</c> constructor.</returns>
    internal static bool TryBindConstructor(
        SemanticModel model,
        ObjectCreationExpressionSyntax creation,
        INamedTypeSymbol regexType,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out IMethodSymbol? constructor)
    {
        if (model.GetSymbolInfo(creation, cancellationToken).Symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } bound
            && SymbolEqualityComparer.Default.Equals(bound.ContainingType, regexType))
        {
            constructor = bound;
            return true;
        }

        constructor = null;
        return false;
    }

    /// <summary>Returns whether an invocation binds to a static method of the resolved <c>Regex</c> type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation.</param>
    /// <param name="regexType">The compilation's <c>Regex</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="method">The bound static method, when it belongs to <paramref name="regexType"/>.</param>
    /// <returns><see langword="true"/> for a static <c>Regex</c> method.</returns>
    internal static bool TryBindStaticMethod(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol regexType,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out IMethodSymbol? method)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { IsStatic: true } bound
            && SymbolEqualityComparer.Default.Equals(bound.ContainingType, regexType))
        {
            method = bound;
            return true;
        }

        method = null;
        return false;
    }
}
