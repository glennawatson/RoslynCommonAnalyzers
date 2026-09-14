// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Resolves a diagnostic reported on a modifier to the member declaration whose modifier list holds it.</summary>
internal static class ReportedModifier
{
    /// <summary>Finds the modifier token a diagnostic starts at and the member declaration that owns it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="declaration">The member declaration enclosing the reported token.</param>
    /// <param name="token">The reported token.</param>
    /// <param name="index">The token's index in the declaration's modifier list, or -1 when it is not one of them.</param>
    /// <returns><see langword="true"/> when the reported token is one of the enclosing member's modifiers.</returns>
    internal static bool TryFind(
        SyntaxNode root,
        Diagnostic diagnostic,
        [NotNullWhen(true)] out MemberDeclarationSyntax? declaration,
        out SyntaxToken token,
        out int index)
    {
        token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        declaration = token.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        if (declaration is null)
        {
            index = -1;
            return false;
        }

        index = declaration.Modifiers.IndexOf(token);
        return index >= 0;
    }
}
