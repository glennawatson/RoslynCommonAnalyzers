// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Version-tolerant detection of C# 14 extension blocks (<c>extension(Receiver) { … }</c>). An
/// extension block parses as a <see cref="TypeDeclarationSyntax"/> whose kind is none of the standard
/// type kinds, so it is recognised structurally without naming the <c>ExtensionBlockDeclaration</c>
/// syntax kind (which does not exist on the Roslyn 4.8 floor, where the syntax cannot occur at all).
/// </summary>
internal static class ExtensionBlockHelper
{
    /// <summary>Returns whether the node is a C# 14 extension block.</summary>
    /// <param name="node">The candidate node.</param>
    /// <returns><see langword="true"/> when the node is an extension block.</returns>
    public static bool IsExtensionBlock(SyntaxNode node)
        => node is TypeDeclarationSyntax type
            && type.Kind() is not (
                SyntaxKind.ClassDeclaration
                or SyntaxKind.StructDeclaration
                or SyntaxKind.RecordDeclaration
                or SyntaxKind.RecordStructDeclaration
                or SyntaxKind.InterfaceDeclaration);

    /// <summary>Returns the textual receiver type of an extension block, or <see langword="null"/> when absent.</summary>
    /// <param name="extensionBlock">The extension block.</param>
    /// <returns>The receiver parameter's type text (for example <c>string</c>), or <see langword="null"/>.</returns>
    public static string? ReceiverTypeText(TypeDeclarationSyntax extensionBlock)
        => extensionBlock.ParameterList?.Parameters is { Count: > 0 } parameters ? parameters[0].Type?.ToString() : null;

    /// <summary>Returns whether the member is a classic <c>this</c>-parameter extension method.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member is a static extension method declared the pre-C#14 way.</returns>
    public static bool IsClassicExtensionMethod(MemberDeclarationSyntax member)
        => member is MethodDeclarationSyntax method
            && method.ParameterList.Parameters.Count > 0
            && method.ParameterList.Parameters[0].Modifiers.Any(SyntaxKind.ThisKeyword);
}
