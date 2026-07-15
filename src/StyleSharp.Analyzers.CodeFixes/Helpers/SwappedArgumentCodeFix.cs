// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Shared rewrite for the code fixes that put two transposed arguments back in order. The analyzer stores
/// the position the reported argument belongs in under a diagnostic property; the only thing that differs
/// between the fixes is which property key carries it, so each fix passes just that key.
/// </summary>
internal static class SwappedArgumentCodeFix
{
    /// <summary>Resolves the reported argument and swaps it with the position the diagnostic points to.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="swapWithKey">The diagnostic property key carrying the partner position.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    public static NodeReplacement? TryBuildSwap(SyntaxNode root, Diagnostic diagnostic, string swapWithKey)
    {
        if (!TryGetPartner(diagnostic, swapWithKey, out var partner)
            || root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<ArgumentSyntax>() is not { } argument
            || argument.Parent is not ArgumentListSyntax list)
        {
            return null;
        }

        var index = list.Arguments.IndexOf(argument);
        if (!IsSwappablePair(list, index, partner))
        {
            return null;
        }

        return new NodeReplacement(list, Swap(list, index, partner), current => Reapply(current, index, partner));
    }

    /// <summary>Returns whether both positions still exist in the list and are distinct.</summary>
    /// <param name="list">The argument list.</param>
    /// <param name="index">The reported argument's position.</param>
    /// <param name="partner">The position it belongs in.</param>
    /// <returns><see langword="true"/> when the swap can be applied.</returns>
    public static bool IsSwappablePair(ArgumentListSyntax list, int index, int partner)
        => index >= 0
            && partner >= 0
            && index != partner
            && index < list.Arguments.Count
            && partner < list.Arguments.Count;

    /// <summary>Exchanges the expressions at two positions, leaving each argument's trivia where it was.</summary>
    /// <param name="list">The argument list.</param>
    /// <param name="index">The reported argument's position.</param>
    /// <param name="partner">The position it belongs in.</param>
    /// <returns>The reordered argument list.</returns>
    public static ArgumentListSyntax Swap(ArgumentListSyntax list, int index, int partner)
    {
        var arguments = list.Arguments;
        var first = arguments[index];
        var second = arguments[partner];
        var swappedFirst = first.WithExpression(second.Expression.WithTriviaFrom(first.Expression));
        var swappedSecond = second.WithExpression(first.Expression.WithTriviaFrom(second.Expression));

        // Replace by position, re-reading the second argument from the list the first replacement produced:
        // the nodes of the original list do not belong to it any more.
        var swapped = arguments.Replace(arguments[index], swappedFirst);
        swapped = swapped.Replace(swapped[partner], swappedSecond);
        return list.WithArguments(swapped);
    }

    /// <summary>Re-applies the swap to the argument list as it stands after any nested batch edit.</summary>
    /// <param name="current">The current argument list.</param>
    /// <param name="index">The reported argument's position.</param>
    /// <param name="partner">The position it belongs in.</param>
    /// <returns>The reordered list, or the node unchanged when it no longer matches.</returns>
    private static SyntaxNode Reapply(SyntaxNode current, int index, int partner)
        => current is ArgumentListSyntax list && IsSwappablePair(list, index, partner)
            ? Swap(list, index, partner)
            : current;

    /// <summary>Reads the partner position carried by the diagnostic.</summary>
    /// <param name="diagnostic">The diagnostic to read.</param>
    /// <param name="swapWithKey">The property key carrying the partner position.</param>
    /// <param name="partner">The transposed partner's position.</param>
    /// <returns><see langword="true"/> when the diagnostic carries a usable position.</returns>
    private static bool TryGetPartner(Diagnostic diagnostic, string swapWithKey, out int partner)
    {
        partner = -1;
        return diagnostic.Properties.TryGetValue(swapWithKey, out var value)
            && value is not null
            && int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out partner);
    }
}
