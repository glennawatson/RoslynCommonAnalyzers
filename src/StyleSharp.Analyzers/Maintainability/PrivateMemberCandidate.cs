// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A private member that may be unused or never read, and the uses found for it.</summary>
internal sealed class PrivateMemberCandidate
{
    /// <summary>Initializes a new instance of the <see cref="PrivateMemberCandidate"/> class.</summary>
    /// <param name="symbol">The declared member symbol.</param>
    /// <param name="declaration">The member declaration syntax.</param>
    /// <param name="identifier">The declaration identifier.</param>
    /// <param name="isFieldLike">Whether reads and writes are tracked separately.</param>
    internal PrivateMemberCandidate(ISymbol symbol, MemberDeclarationSyntax declaration, SyntaxToken identifier, bool isFieldLike)
    {
        Symbol = symbol;
        Declaration = declaration;
        Identifier = identifier;
        IsFieldLike = isFieldLike;
    }

    /// <summary>Gets the declared member symbol.</summary>
    internal ISymbol Symbol { get; }

    /// <summary>Gets the declaration syntax.</summary>
    internal MemberDeclarationSyntax Declaration { get; }

    /// <summary>Gets the declaration identifier.</summary>
    internal SyntaxToken Identifier { get; }

    /// <summary>Gets a value indicating whether reads and writes are tracked separately.</summary>
    internal bool IsFieldLike { get; }

    /// <summary>Gets or sets a value indicating whether the member is read or otherwise used.</summary>
    internal bool Read { get; set; }

    /// <summary>Gets or sets a value indicating whether the member is written.</summary>
    internal bool Written { get; set; }
}
