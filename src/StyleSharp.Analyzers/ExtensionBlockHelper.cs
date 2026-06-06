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

    /// <summary>Returns the receiver type syntax of an extension block, or <see langword="null"/> when absent.</summary>
    /// <param name="extensionBlock">The extension block.</param>
    /// <returns>The receiver parameter's type syntax, or <see langword="null"/>.</returns>
    public static TypeSyntax? ReceiverType(TypeDeclarationSyntax extensionBlock)
        => extensionBlock.ParameterList?.Parameters is { Count: > 0 } parameters ? parameters[0].Type : null;

    /// <summary>Returns the textual receiver type of an extension block, or <see langword="null"/> when absent.</summary>
    /// <param name="extensionBlock">The extension block.</param>
    /// <returns>The receiver parameter's type text (for example <c>string</c>), or <see langword="null"/>.</returns>
    public static string? ReceiverTypeText(TypeDeclarationSyntax extensionBlock)
        => ReceiverTypeText(ReceiverType(extensionBlock));

    /// <summary>Returns the textual receiver type for a receiver type syntax.</summary>
    /// <param name="receiverType">The receiver type syntax.</param>
    /// <returns>The receiver text, or <see langword="null"/> when absent.</returns>
    public static string? ReceiverTypeText(TypeSyntax? receiverType) => receiverType?.ToString();

    /// <summary>Returns whether a receiver type is one that attaches extension members to every type.</summary>
    /// <param name="receiverType">The receiver type syntax.</param>
    /// <param name="receiverText">The display text used in the diagnostic message.</param>
    /// <returns><see langword="true"/> for <c>object</c>, <c>System.Object</c>, or <c>dynamic</c>.</returns>
    public static bool IsBroadReceiver(TypeSyntax? receiverType, out string receiverText)
    {
        receiverText = string.Empty;
        switch (receiverType)
        {
            case null:
                return false;

            case PredefinedTypeSyntax predefined when predefined.Keyword.IsKind(SyntaxKind.ObjectKeyword):
            {
                receiverText = "object";
                return true;
            }

            case IdentifierNameSyntax identifier when identifier.Identifier.ValueText == "dynamic":
            {
                receiverText = "dynamic";
                return true;
            }

            case QualifiedNameSyntax qualified
                when qualified.Left is IdentifierNameSyntax { Identifier.ValueText: "System" }
                && qualified.Right.Identifier.ValueText == "Object":
            {
                receiverText = "System.Object";
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>Returns whether the member is a classic <c>this</c>-parameter extension method.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member is a static extension method declared the pre-C#14 way.</returns>
    public static bool IsClassicExtensionMethod(MemberDeclarationSyntax member)
        => member is MethodDeclarationSyntax method
            && method.ParameterList.Parameters.Count > 0
            && ModifierListHelper.Contains(method.ParameterList.Parameters[0].Modifiers, SyntaxKind.ThisKeyword);
}
