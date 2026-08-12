// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Decides whether a call could have carried a cancellation token, and which parameter slot it would
/// go in. Two shapes qualify: the called method already has a token parameter that the call left at
/// its default, or the called method has none and a sibling overload takes one.
/// </summary>
/// <remarks>
/// The overload is proved, never guessed. A candidate qualifies only when it is declared on the same
/// type, agrees on staticness, returns exactly the same type, and accepts the very arguments already
/// written — its parameters, minus the token slot, must lead with the called method's parameters and
/// add nothing that is not optional. That is what keeps the suggestion honest on a framework where the
/// cancellable overload may simply not exist. Resolutions are memoized per method symbol, so a file
/// that calls the same method a hundred times pays for the search once.
/// </remarks>
internal static class CancellationTokenOverload
{
    /// <summary>Finds the token a call should have passed, and the method that would receive it.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The call being inspected.</param>
    /// <param name="tokenType">The cancellation token type resolved for the compilation.</param>
    /// <param name="cache">The per-compilation resolution cache, or <see langword="null"/> to resolve without memoizing.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The forwardable token, or <see langword="null"/> when the call already carries one or has no cancellable form.</returns>
    public static ForwardableToken? TryFind(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol tokenType,
        ConcurrentDictionary<ISymbol, TokenTarget?>? cache,
        CancellationToken cancellationToken)
    {
        if (CancellationTokenScope.TryFindInScope(invocation) is not { } parameter
            || model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol called)
        {
            return null;
        }

        if (Resolve(called, tokenType, cache) is not { } target)
        {
            return null;
        }

        var isSameMethod = SymbolEqualityComparer.Default.Equals(target.Method, called);
        if (isSameMethod && IsSupplied(invocation.ArgumentList.Arguments, target.Method.Parameters[target.TokenIndex].Name, target.TokenIndex))
        {
            return null;
        }

        if (!isSameMethod && !model.IsAccessible(invocation.SpanStart, target.Method))
        {
            return null;
        }

        return CancellationTokenScope.TryResolveName(model, parameter, tokenType, cancellationToken) is { } name
            ? new ForwardableToken(target.Method, target.TokenIndex, name)
            : null;
    }

    /// <summary>Resolves a call's token target, reusing the per-compilation cache when one was supplied.</summary>
    /// <param name="called">The bound method.</param>
    /// <param name="tokenType">The cancellation token type resolved for the compilation.</param>
    /// <param name="cache">The per-compilation resolution cache, or <see langword="null"/> to resolve without memoizing.</param>
    /// <returns>The token target, or <see langword="null"/> when the call has no cancellable form.</returns>
    private static TokenTarget? Resolve(IMethodSymbol called, INamedTypeSymbol tokenType, ConcurrentDictionary<ISymbol, TokenTarget?>? cache)
    {
        if (cache is null)
        {
            return TryResolveTarget(called, tokenType);
        }

        if (!cache.TryGetValue(called, out var cached))
        {
            cached = TryResolveTarget(called, tokenType);
            cache.TryAdd(called, cached);
        }

        return cached;
    }

    /// <summary>Resolves the method that would receive the token: the called one, or a sibling overload.</summary>
    /// <param name="called">The bound method.</param>
    /// <param name="tokenType">The cancellation token type resolved for the compilation.</param>
    /// <returns>The token target, or <see langword="null"/> when neither shape applies.</returns>
    private static TokenTarget? TryResolveTarget(IMethodSymbol called, INamedTypeSymbol tokenType)
    {
        var index = IndexOfSoleToken(called.Parameters, tokenType);
        if (index >= 0)
        {
            // A required token parameter was necessarily supplied, or the call would not have bound.
            return called.Parameters[index].IsOptional ? new TokenTarget(called, index) : null;
        }

        // An overload search compares against the sibling's own parameter list, which only lines up with the
        // call's arguments for an ordinary, non-generic method: a reduced extension method hides its receiver,
        // and a generic method's type parameters belong to the method that declared them.
        return called.MethodKind == MethodKind.Ordinary && !called.IsGenericMethod
            ? TryResolveOverload(called, tokenType)
            : null;
    }

    /// <summary>Searches the called method's type for an overload that takes a token and accepts the same arguments.</summary>
    /// <param name="called">The bound method.</param>
    /// <param name="tokenType">The cancellation token type resolved for the compilation.</param>
    /// <returns>The overload target, or <see langword="null"/> when the type has none that fits.</returns>
    private static TokenTarget? TryResolveOverload(IMethodSymbol called, INamedTypeSymbol tokenType)
    {
        var candidates = called.ContainingType.GetMembers(called.Name);
        for (var i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] is not IMethodSymbol candidate || !IsSubstitutable(candidate, called))
            {
                continue;
            }

            var index = IndexOfSoleToken(candidate.Parameters, tokenType);
            if (index >= 0 && AcceptsSameArguments(called.Parameters, candidate.Parameters, index))
            {
                return new TokenTarget(candidate, index);
            }
        }

        return null;
    }

    /// <summary>Returns whether a same-named sibling could stand in for the called method at all.</summary>
    /// <param name="candidate">The sibling being considered.</param>
    /// <param name="called">The bound method.</param>
    /// <returns><see langword="true"/> when the two agree on everything but their parameters.</returns>
    private static bool IsSubstitutable(IMethodSymbol candidate, IMethodSymbol called)
        => !SymbolEqualityComparer.Default.Equals(candidate, called)
            && candidate.MethodKind == MethodKind.Ordinary
            && !candidate.IsGenericMethod
            && candidate.IsStatic == called.IsStatic
            && candidate.IsExtensionMethod == called.IsExtensionMethod
            && candidate.RefKind == called.RefKind
            && SymbolEqualityComparer.Default.Equals(candidate.ReturnType, called.ReturnType);

    /// <summary>Returns whether the arguments already written still bind to a candidate overload.</summary>
    /// <param name="called">The bound method's parameters.</param>
    /// <param name="candidate">The candidate overload's parameters.</param>
    /// <param name="tokenIndex">The candidate's token parameter index.</param>
    /// <returns><see langword="true"/> when the candidate leads with the same parameters and adds only optional ones.</returns>
    private static bool AcceptsSameArguments(ImmutableArray<IParameterSymbol> called, ImmutableArray<IParameterSymbol> candidate, int tokenIndex)
    {
        if (candidate.Length - 1 < called.Length)
        {
            return false;
        }

        var matched = 0;
        for (var i = 0; i < candidate.Length; i++)
        {
            if (i == tokenIndex)
            {
                continue;
            }

            if (matched == called.Length)
            {
                if (!candidate[i].IsOptional)
                {
                    return false;
                }

                continue;
            }

            var expected = called[matched];
            var actual = candidate[i];
            if (expected.RefKind != actual.RefKind
                || expected.IsParams != actual.IsParams
                || !SymbolEqualityComparer.Default.Equals(expected.Type, actual.Type))
            {
                return false;
            }

            matched++;
        }

        return matched == called.Length;
    }

    /// <summary>Finds the one parameter of the cancellation token type.</summary>
    /// <param name="parameters">The parameter list to scan.</param>
    /// <param name="tokenType">The cancellation token type resolved for the compilation.</param>
    /// <returns>The parameter's index, or <c>-1</c> when the list has no token parameter or more than one.</returns>
    private static int IndexOfSoleToken(ImmutableArray<IParameterSymbol> parameters, INamedTypeSymbol tokenType)
    {
        var index = -1;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(parameters[i].Type, tokenType))
            {
                continue;
            }

            if (index >= 0)
            {
                return -1;
            }

            index = i;
        }

        return index;
    }

    /// <summary>Returns whether a call already fills a parameter slot, positionally or by name.</summary>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="name">The parameter's name.</param>
    /// <param name="index">The parameter's index.</param>
    /// <returns><see langword="true"/> when the slot is filled, so nothing is dropped.</returns>
    private static bool IsSupplied(SeparatedSyntaxList<ArgumentSyntax> arguments, string name, int index)
    {
        if (arguments.Count > index)
        {
            return true;
        }

        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon?.Name.Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The method that would receive a cancellation token, and the slot it goes in.</summary>
    /// <param name="Method">The method to call — the one already called, or the overload that takes a token.</param>
    /// <param name="TokenIndex">The index of that method's cancellation token parameter.</param>
    internal readonly record struct TokenTarget(IMethodSymbol Method, int TokenIndex);

    /// <summary>A token in scope at a call site, with the method and slot that would receive it.</summary>
    /// <param name="Method">The method to call — the one already called, or the overload that takes a token.</param>
    /// <param name="TokenIndex">The index of that method's cancellation token parameter.</param>
    /// <param name="TokenName">The name of the token parameter in scope.</param>
    internal readonly record struct ForwardableToken(IMethodSymbol Method, int TokenIndex, string TokenName);
}
