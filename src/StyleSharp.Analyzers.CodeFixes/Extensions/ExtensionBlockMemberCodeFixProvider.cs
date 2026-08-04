// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Moves a classic <c>this</c>-parameter extension method into an <c>extension(Receiver) { … }</c>
/// block (SST1703, SST1705).
/// </summary>
/// <remarks>
/// The block is built by parsing its text rather than through the typed factory, because the
/// <c>ExtensionBlockDeclaration</c> syntax kind does not exist on the Roslyn 4.8 floor this assembly
/// also builds against. Where the host parser is too old to understand the syntax the parse fails and
/// no fix is offered — which is also the only place the syntax could not have been used anyway.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExtensionBlockMemberCodeFixProvider))]
[Shared]
public sealed class ExtensionBlockMemberCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ExtensionRules.PreferExtensionBlock.Id,
        ExtensionRules.DoNotMixExtensionStyles.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Move into an extension block",
            nameof(ExtensionBlockMemberCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Rewrites the containing class so the reported method lives in an extension block.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape cannot be converted.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } method
            || method.Parent is not ClassDeclarationSyntax containingClass
            || !IsConvertible(method)
            || method.ParameterList.Parameters[0] is not { Type: { } receiverType } receiver)
        {
            return null;
        }

        var receiverName = receiver.Identifier.ValueText;
        var member = ToExtensionMember(method).WithAdditionalAnnotations(Formatter.Annotation);
        if (FindMatchingBlock(containingClass, receiverType, receiverName) is { } existing)
        {
            return MergeIntoBlock(containingClass, existing, method, member);
        }

        if (ParseExtensionBlock(receiverType, receiverName) is not { } block)
        {
            return null;
        }

        var introduced = block.AddMembers(member).WithTriviaFrom(method).WithAdditionalAnnotations(Formatter.Annotation);
        return new NodeReplacement(containingClass, containingClass.ReplaceNode(method, introduced));
    }

    /// <summary>Adds the member to an existing block and drops the method it came from.</summary>
    /// <param name="containingClass">The static class holding the extensions.</param>
    /// <param name="block">The block that already declares this receiver.</param>
    /// <param name="method">The classic extension method being moved.</param>
    /// <param name="member">The method rewritten as a block member.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when either node is no longer a member.</returns>
    /// <remarks>
    /// Both edits are made to the member list in one step. Doing them as two tree rewrites would leave the
    /// second one holding a node from a tree that no longer exists, and it also keeps the blank line that
    /// separated the two members visible after the first is gone.
    /// </remarks>
    private static NodeReplacement? MergeIntoBlock(
        ClassDeclarationSyntax containingClass,
        TypeDeclarationSyntax block,
        MethodDeclarationSyntax method,
        MethodDeclarationSyntax member)
    {
        var members = containingClass.Members;
        var methodIndex = members.IndexOf(method);
        var blockIndex = members.IndexOf(block);
        if (methodIndex < 0 || blockIndex < 0)
        {
            return null;
        }

        var updatedBlock = block.AddMembers(member);

        // The first member carries the layout that follows the opening brace, so hand it to whichever
        // member inherits that position.
        if (methodIndex == 0 && blockIndex > methodIndex)
        {
            updatedBlock = updatedBlock.WithLeadingTrivia(method.GetLeadingTrivia());
        }

        var updated = members.Replace(block, updatedBlock).RemoveAt(methodIndex);
        return new NodeReplacement(containingClass, containingClass.WithMembers(updated));
    }

    /// <summary>Returns whether a classic extension method can be moved without further judgement.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns><see langword="true"/> when the move is mechanical.</returns>
    /// <remarks>
    /// A generic method's type parameters may belong on the block or on the member depending on which
    /// mention the receiver, and a receiver carrying attributes or a default has no equivalent on the
    /// block's parameter. Those need a decision, so no fix is offered for them.
    /// </remarks>
    private static bool IsConvertible(MethodDeclarationSyntax method)
        => ExtensionBlockHelper.IsClassicExtensionMethod(method)
        && method.TypeParameterList is null
        && method.ConstraintClauses.Count == 0
        && method.ParameterList.Parameters[0] is { AttributeLists.Count: 0, Default: null, Type: not null }
        && (method.Body is not null || method.ExpressionBody is not null);

    /// <summary>Returns the extension block in the class that already declares this receiver, if any.</summary>
    /// <param name="containingClass">The static class holding the extensions.</param>
    /// <param name="receiverType">The receiver type of the method being moved.</param>
    /// <param name="receiverName">The receiver parameter name the method's body refers to.</param>
    /// <returns>The matching block, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The name has to match as well as the type: the moved body refers to the receiver by the name the
    /// method gave it, and a block declaring the same type under a different name would not compile.
    /// </remarks>
    private static TypeDeclarationSyntax? FindMatchingBlock(ClassDeclarationSyntax containingClass, TypeSyntax receiverType, string receiverName)
    {
        var receiverText = ExtensionBlockHelper.ReceiverTypeText(receiverType);
        foreach (var member in containingClass.Members)
        {
            if (!ExtensionBlockHelper.IsExtensionBlock(member)
                || member is not TypeDeclarationSyntax block
                || block.ParameterList?.Parameters is not { Count: 1 } parameters
                || parameters[0].Identifier.ValueText != receiverName
                || ExtensionBlockHelper.ReceiverTypeText(block) != receiverText)
            {
                continue;
            }

            return block;
        }

        return null;
    }

    /// <summary>Parses an empty extension block for a receiver.</summary>
    /// <param name="receiverType">The receiver type.</param>
    /// <param name="receiverName">The receiver parameter name.</param>
    /// <returns>The parsed block, or <see langword="null"/> when the host parser does not accept it.</returns>
    private static TypeDeclarationSyntax? ParseExtensionBlock(TypeSyntax receiverType, string receiverName)
    {
        var parsed = SyntaxFactory.ParseMemberDeclaration($"extension({receiverType} {receiverName})\n{{\n}}\n");
        return parsed is TypeDeclarationSyntax block
            && ExtensionBlockHelper.IsExtensionBlock(block)
            && !parsed.ContainsDiagnostics
                ? block
                : null;
    }

    /// <summary>Rewrites a classic extension method as an extension-block member.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns>The member as it is declared inside the block.</returns>
    /// <remarks>
    /// Inside a block the receiver is the block's parameter, so the method drops its own receiver
    /// parameter and its <c>static</c> modifier; everything else — attributes, documentation, the body —
    /// moves across untouched.
    /// </remarks>
    private static MethodDeclarationSyntax ToExtensionMember(MethodDeclarationSyntax method)
    {
        var parameters = method.ParameterList.Parameters.RemoveAt(0);

        return method
            .WithModifiers(WithoutStatic(method.Modifiers))
            .WithParameterList(method.ParameterList.WithParameters(parameters));
    }

    /// <summary>Removes the <c>static</c> modifier, keeping the member's leading trivia in place.</summary>
    /// <param name="modifiers">The method's modifiers.</param>
    /// <returns>The modifiers without <c>static</c>.</returns>
    private static SyntaxTokenList WithoutStatic(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (!modifiers[i].IsKind(SyntaxKind.StaticKeyword))
            {
                continue;
            }

            var remaining = modifiers.RemoveAt(i);

            // The first modifier carries the member's documentation and indentation, so pass them on
            // when 'static' was the one holding them.
            return i == 0 && remaining.Count > 0
                ? remaining.Replace(remaining[0], remaining[0].WithLeadingTrivia(modifiers[i].LeadingTrivia))
                : remaining;
        }

        return modifiers;
    }
}
