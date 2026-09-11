// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>Moves a classic <c>this</c>-parameter extension method into an <c>extension(Receiver) { … }</c> block (SST1703, SST1705).</summary>
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
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Move into an extension block",
            nameof(ExtensionBlockMemberCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    [global::System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Rewrites the containing class so the reported method lives in an extension block.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape cannot be converted.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        // The method leaves the class's member list and reappears inside a block elsewhere in it, so a
        // directive among the members would lose the half that sits on the method.
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } method
            || method.Parent is not ClassDeclarationSyntax containingClass
            || DirectiveBoundaries.SeparateMembers(containingClass)
            || !IsConvertible(method)
            || method.ParameterList.Parameters[0] is not { Type: { } receiverType } receiver)
        {
            return null;
        }

        var receiverName = receiver.Identifier.ValueText;
        var receiverModifiers = ReceiverModifierText(receiver.Modifiers);
        var member = ToExtensionMember(method).WithAdditionalAnnotations(Formatter.Annotation);
        if (FindMatchingBlock(containingClass, receiverType, receiverName, receiverModifiers) is { } existing)
        {
            return MergeIntoBlock(containingClass, existing, method, member);
        }

        if (ParseExtensionBlock(receiverType, receiverName, receiverModifiers) is not { } block)
        {
            return null;
        }

        var introduced = block.AddMembers(member)
            .WithLeadingTrivia(LayoutTriviaOf(method))
            .WithTrailingTrivia(method.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);
        return new NodeReplacement(containingClass, containingClass.ReplaceNode(method, introduced));
    }

    /// <summary>Gets a method's leading trivia without its documentation comment.</summary>
    /// <param name="method">The classic extension method being moved.</param>
    /// <returns>The blank lines and indentation that position the declaration.</returns>
    /// <remarks>
    /// The documentation travels with the member into the block. Leaving a copy on the block itself
    /// documents parameters the block does not declare (CS1572).
    /// </remarks>
    private static SyntaxTriviaList LayoutTriviaOf(MethodDeclarationSyntax method)
    {
        var kept = new List<SyntaxTrivia>();
        foreach (var trivia in method.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                kept.Add(trivia);
            }
        }

        return SyntaxFactory.TriviaList(kept);
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
            updatedBlock = updatedBlock.WithLeadingTrivia(LayoutTriviaOf(method));
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
    private static bool IsConvertible(MethodDeclarationSyntax method) =>
        ExtensionBlockHelper.IsClassicExtensionMethod(method)
        && method.TypeParameterList is null
        && method.ConstraintClauses.Count == 0
        && method.ParameterList.Parameters[0] is { AttributeLists.Count: 0, Default: null, Type: not null }
        && (method.Body is not null || method.ExpressionBody is not null);

    /// <summary>Returns the extension block in the class that already declares this receiver, if any.</summary>
    /// <param name="containingClass">The static class holding the extensions.</param>
    /// <param name="receiverType">The receiver type of the method being moved.</param>
    /// <param name="receiverName">The receiver parameter name the method's body refers to.</param>
    /// <param name="receiverModifiers">How the method takes its receiver, without <c>this</c>.</param>
    /// <returns>The matching block, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The name has to match as well as the type: the moved body refers to the receiver by the name the
    /// method gave it, and a block declaring the same type under a different name would not compile. How
    /// the receiver is passed has to match too — a by-value block cannot host a method that took its
    /// receiver by readonly reference.
    /// </remarks>
    private static TypeDeclarationSyntax? FindMatchingBlock(
        ClassDeclarationSyntax containingClass,
        TypeSyntax receiverType,
        string receiverName,
        string receiverModifiers)
    {
        var receiverText = ExtensionBlockHelper.ReceiverTypeText(receiverType);
        foreach (var member in containingClass.Members)
        {
            if (!ExtensionBlockHelper.IsExtensionBlock(member)
                || member is not TypeDeclarationSyntax block
                || block.ParameterList?.Parameters is not { Count: 1 } parameters
                || parameters[0].Identifier.ValueText != receiverName
                || ReceiverModifierText(parameters[0].Modifiers) != receiverModifiers
                || ExtensionBlockHelper.ReceiverTypeText(block) != receiverText)
            {
                continue;
            }

            return block;
        }

        return null;
    }

    /// <summary>Renders how a receiver is passed, leaving out the <c>this</c> that marks the method.</summary>
    /// <param name="modifiers">The receiver parameter's modifiers.</param>
    /// <returns>The remaining modifiers in source order, space separated.</returns>
    /// <remarks>
    /// <c>this in T</c> passes a large readonly struct by readonly reference, and dropping the <c>in</c>
    /// would copy it at every call. The block's parameter carries the same modifiers the method did.
    /// </remarks>
    private static string ReceiverModifierText(in SyntaxTokenList modifiers)
    {
        var rendered = new StringBuilder();
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.ThisKeyword))
            {
                continue;
            }

            if (rendered.Length > 0)
            {
                rendered.Append(' ');
            }

            rendered.Append(modifiers[i].ValueText);
        }

        return rendered.ToString();
    }

    /// <summary>Parses an empty extension block for a receiver.</summary>
    /// <param name="receiverType">The receiver type.</param>
    /// <param name="receiverName">The receiver parameter name.</param>
    /// <param name="receiverModifiers">How the method takes its receiver, without <c>this</c>.</param>
    /// <returns>The parsed block, or <see langword="null"/> when the host parser does not accept it.</returns>
    /// <remarks>
    /// A modifier the language does not allow on a block's receiver comes back from the parser as a
    /// diagnostic, which declines the fix rather than writing something that will not compile.
    /// </remarks>
    private static TypeDeclarationSyntax? ParseExtensionBlock(TypeSyntax receiverType, string receiverName, string receiverModifiers)
    {
        var prefix = receiverModifiers.Length == 0 ? string.Empty : receiverModifiers + " ";
        var parsed = SyntaxFactory.ParseMemberDeclaration($"extension({prefix}{receiverType} {receiverName})\n{{\n}}\n");
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
    /// parameter, that parameter's documentation, and its <c>static</c> modifier; everything else —
    /// attributes, the rest of the documentation, the body — moves across untouched.
    /// </remarks>
    private static MethodDeclarationSyntax ToExtensionMember(MethodDeclarationSyntax method)
    {
        var receiverName = method.ParameterList.Parameters[0].Identifier.ValueText;
        var parameters = method.ParameterList.Parameters.RemoveAt(0);

        // The trivia goes on last: replacing the modifiers restores the tokens' own leading trivia,
        // which still carries the documentation this strips.
        return method
            .WithModifiers(WithoutStatic(method.Modifiers))
            .WithParameterList(method.ParameterList.WithParameters(parameters))
            .WithLeadingTrivia(WithoutParameterDocumentation(method.GetLeadingTrivia(), receiverName));
    }

    /// <summary>Removes one <c>&lt;param&gt;</c> element from a declaration's documentation comment.</summary>
    /// <param name="trivia">The declaration's leading trivia.</param>
    /// <param name="parameterName">The parameter whose documentation should go.</param>
    /// <returns>The trivia with that element removed.</returns>
    /// <remarks>
    /// The receiver becomes the block's parameter, so documenting it on the member describes a parameter
    /// the member no longer declares (CS1572).
    /// </remarks>
    private static SyntaxTriviaList WithoutParameterDocumentation(SyntaxTriviaList trivia, string parameterName)
    {
        for (var i = 0; i < trivia.Count; i++)
        {
            if (trivia[i].GetStructure() is not DocumentationCommentTriviaSyntax documentation)
            {
                continue;
            }

            var kept = documentation.Content;
            for (var j = kept.Count - 1; j >= 0; j--)
            {
                if (kept[j] is XmlElementSyntax { StartTag.Name.LocalName.ValueText: "param" } element
                    && NamesParameter(element, parameterName))
                {
                    kept = RemoveWithPrecedingExterior(kept, j);
                }
            }

            if (kept.Count == documentation.Content.Count)
            {
                continue;
            }

            return trivia.Replace(trivia[i], SyntaxFactory.Trivia(documentation.WithContent(kept)));
        }

        return trivia;
    }

    /// <summary>Returns whether a <c>&lt;param&gt;</c> element carries the given name attribute.</summary>
    /// <param name="element">The documentation element.</param>
    /// <param name="parameterName">The parameter name to match.</param>
    /// <returns><see langword="true"/> when the element documents that parameter.</returns>
    private static bool NamesParameter(XmlElementSyntax element, string parameterName)
    {
        foreach (var attribute in element.StartTag.Attributes)
        {
            if (attribute is XmlNameAttributeSyntax name && name.Identifier.Identifier.ValueText == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Removes a documentation element together with the <c>///</c> run that introduces it.</summary>
    /// <param name="content">The documentation content.</param>
    /// <param name="index">The element to remove.</param>
    /// <returns>The content without that element or its exterior text.</returns>
    private static SyntaxList<XmlNodeSyntax> RemoveWithPrecedingExterior(SyntaxList<XmlNodeSyntax> content, int index)
    {
        var withoutElement = content.RemoveAt(index);
        var previous = index - 1;
        return previous >= 0 && withoutElement.Count > previous && withoutElement[previous] is XmlTextSyntax
            ? withoutElement.RemoveAt(previous)
            : withoutElement;
    }

    /// <summary>Removes the <c>static</c> modifier, keeping the member's leading trivia in place.</summary>
    /// <param name="modifiers">The method's modifiers.</param>
    /// <returns>The modifiers without <c>static</c>.</returns>
    private static SyntaxTokenList WithoutStatic(in SyntaxTokenList modifiers)
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
