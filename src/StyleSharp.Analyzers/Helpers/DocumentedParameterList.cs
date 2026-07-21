// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Extracts the parameters a documentation comment can describe with <c>&lt;param&gt;</c> elements
/// from the member that owns the comment — methods, constructors, delegates, indexers, and the
/// primary constructor of a type declaration. Kept in one place so the SST1660 analyzer and its
/// code fix agree on which parameters exist and in what order.
/// </summary>
internal static class DocumentedParameterList
{
    /// <summary>Returns the parameters a member exposes to <c>&lt;param&gt;</c> documentation.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The member's parameters, or an empty list when it declares none that a <c>&lt;param&gt;</c> would describe.</returns>
    public static SeparatedSyntaxList<ParameterSyntax> Of(SyntaxNode member) => member switch
    {
        MethodDeclarationSyntax method => method.ParameterList.Parameters,
        ConstructorDeclarationSyntax constructor => constructor.ParameterList.Parameters,
        DelegateDeclarationSyntax @delegate => @delegate.ParameterList.Parameters,
        IndexerDeclarationSyntax indexer => indexer.ParameterList.Parameters,
        TypeDeclarationSyntax type => type.ParameterList?.Parameters ?? default,
        _ => default,
    };
}
