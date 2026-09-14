// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads whether a member is written against an explicit interface, as in <c>int IFoo.Bar()</c>. The syntax is
/// decisive on its own: it holds even while the interface type does not bind.
/// </summary>
internal static class ExplicitInterfaceSpecifiers
{
    /// <summary>Returns whether a method, property, indexer or event declaration carries an explicit interface specifier.</summary>
    /// <param name="node">The declaration.</param>
    /// <returns><see langword="true"/> when the member names the interface it implements; <see langword="false"/> for any other node.</returns>
    internal static bool IsPresent(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.ExplicitInterfaceSpecifier is not null,
        PropertyDeclarationSyntax property => property.ExplicitInterfaceSpecifier is not null,
        IndexerDeclarationSyntax indexer => indexer.ExplicitInterfaceSpecifier is not null,
        EventDeclarationSyntax @event => @event.ExplicitInterfaceSpecifier is not null,
        _ => false,
    };
}
