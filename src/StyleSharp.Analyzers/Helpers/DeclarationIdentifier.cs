// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Reads the name a declaration inside a member body introduces into scope.</summary>
internal static class DeclarationIdentifier
{
    /// <summary>Gets the identifier a parameter, variable, <c>foreach</c> variable, catch variable, pattern designation or local function declares.</summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns>The declared identifier, or <see langword="default"/> when the node is not one of those declarations.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SyntaxToken Of(SyntaxNode node) =>
        node switch
        {
            ParameterSyntax parameter => parameter.Identifier,
            VariableDeclaratorSyntax variable => variable.Identifier,
            ForEachStatementSyntax forEach => forEach.Identifier,
            CatchDeclarationSyntax catchDeclaration => catchDeclaration.Identifier,
            SingleVariableDesignationSyntax designation => designation.Identifier,
            LocalFunctionStatementSyntax localFunction => localFunction.Identifier,
            _ => default,
        };
}
