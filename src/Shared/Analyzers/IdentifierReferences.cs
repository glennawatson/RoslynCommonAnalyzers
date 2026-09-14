// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>Finds identifiers within a syntax subtree by name, by parameter, or by the symbol they bind to.</summary>
internal static class IdentifierReferences
{
    /// <summary>Returns whether a node is, or contains, an identifier with the given name.</summary>
    /// <param name="node">The node to search.</param>
    /// <param name="name">The name to look for.</param>
    /// <returns><see langword="true"/> when the name appears.</returns>
    internal static bool MentionsName(SyntaxNode node, string name) =>
        node is IdentifierNameSyntax identifier
            ? identifier.Identifier.ValueText == name
            : !DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, string>(node, ref name, IsOtherName);

    /// <summary>Returns whether a node is, or contains, an identifier naming one of the parameters.</summary>
    /// <param name="node">The node to search.</param>
    /// <param name="parameters">The parameters whose names to look for.</param>
    /// <returns><see langword="true"/> when a parameter's name appears.</returns>
    internal static bool MentionsParameter(SyntaxNode node, ParameterListSyntax parameters) =>
        node is IdentifierNameSyntax identifier
            ? NamesParameter(identifier, parameters)
            : !DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, ParameterListSyntax>(node, ref parameters, IsNotParameter);

    /// <summary>Returns whether a node is, or contains, an identifier that binds to the symbol.</summary>
    /// <param name="node">The node to search.</param>
    /// <param name="symbol">The symbol to find.</param>
    /// <param name="model">The semantic model that binds each same-named identifier.</param>
    /// <param name="cancellationToken">A token that cancels binding.</param>
    /// <returns><see langword="true"/> when the symbol is referenced.</returns>
    internal static bool References(SyntaxNode node, ISymbol symbol, SemanticModel model, CancellationToken cancellationToken)
    {
        if (node is IdentifierNameSyntax identifier)
        {
            return IsReferenceTo(identifier, symbol, model, cancellationToken);
        }

        var search = new SymbolReferenceSearch(symbol, model, cancellationToken);
        return !DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, SymbolReferenceSearch>(node, ref search, IsNotReference);
    }

    /// <summary>Returns whether an identifier binds to the symbol, comparing names before binding.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <param name="symbol">The symbol to match.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels binding.</param>
    /// <returns><see langword="true"/> when the identifier is a reference to the symbol.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsReferenceTo(IdentifierNameSyntax identifier, ISymbol symbol, SemanticModel model, CancellationToken cancellationToken) =>
        identifier.Identifier.ValueText == symbol.Name
            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, symbol);

    /// <summary>Returns whether a node contains an identifier token with the given name, declared names included.</summary>
    /// <param name="node">The node to search.</param>
    /// <param name="name">The name to look for.</param>
    /// <returns><see langword="true"/> when an identifier token spells the name.</returns>
    internal static bool ContainsIdentifierToken(SyntaxNode node, string name) =>
        !DescendantTraversalHelper.VisitDescendantTokens(node, ref name, IsOtherIdentifierToken);

    /// <summary>Returns whether every identifier with the given name beneath a node is the receiver of a member access.</summary>
    /// <param name="node">The node to search.</param>
    /// <param name="name">The name to look for.</param>
    /// <returns><see langword="true"/> when the name is only read through its members.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsOnlyMemberReceiver(SyntaxNode node, string name) =>
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, string>(node, ref name, IsOtherNameOrMemberReceiver);

    /// <summary>Returns whether an identifier names one of the parameters.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <param name="parameters">The parameters.</param>
    /// <returns><see langword="true"/> when the name matches a parameter.</returns>
    private static bool NamesParameter(IdentifierNameSyntax identifier, ParameterListSyntax parameters)
    {
        var name = identifier.Identifier.ValueText;
        var list = parameters.Parameters;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Continues the walk past an identifier with a different name.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="name">The name being looked for.</param>
    /// <returns><see langword="false"/> at the first match, which stops the walk.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOtherName(IdentifierNameSyntax identifier, ref string name) => identifier.Identifier.ValueText != name;

    /// <summary>Continues the walk past an identifier that names no parameter.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="parameters">The parameters being looked for.</param>
    /// <returns><see langword="false"/> at the first match, which stops the walk.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNotParameter(IdentifierNameSyntax identifier, ref ParameterListSyntax parameters) => !NamesParameter(identifier, parameters);

    /// <summary>Continues the walk past an identifier that does not bind to the symbol.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="search">The symbol being looked for.</param>
    /// <returns><see langword="false"/> at the first reference, which stops the walk.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNotReference(IdentifierNameSyntax identifier, ref SymbolReferenceSearch search) =>
        !IsReferenceTo(identifier, search.Symbol, search.Model, search.CancellationToken);

    /// <summary>Continues the walk past a token that is not an identifier with the name.</summary>
    /// <param name="token">The visited token.</param>
    /// <param name="name">The name being looked for.</param>
    /// <returns><see langword="false"/> at the first match, which stops the walk.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOtherIdentifierToken(in SyntaxToken token, ref string name) =>
        !token.IsKind(SyntaxKind.IdentifierToken) || token.ValueText != name;

    /// <summary>Continues the walk past an identifier with a different name, or one read through a member access.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="name">The name being looked for.</param>
    /// <returns><see langword="false"/> at a use that is not a member read, which stops the walk.</returns>
    private static bool IsOtherNameOrMemberReceiver(IdentifierNameSyntax identifier, ref string name) =>
        identifier.Identifier.ValueText != name
            || (identifier.Parent is MemberAccessExpressionSyntax access && access.Expression == identifier);
}
