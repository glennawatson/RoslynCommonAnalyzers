// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
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

    /// <summary>Rewrites a helper whose first parameter is the receiver but is not marked <c>this</c> (SST1709).</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape cannot be converted.</returns>
    /// <remarks>
    /// The conversion is the same one either way — the receiver leaves the parameter list, its documentation
    /// goes with it, and the member joins a block that already declares that receiver when there is one. Only
    /// the test for which methods qualify differs, so both rules share this.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static NodeReplacement? TryRewriteAlmostExtension(SyntaxNode root, Diagnostic diagnostic) =>
        TryRewrite(root, diagnostic, almostExtension: true);

    /// <summary>Rewrites the containing class so the reported method lives in an extension block.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape cannot be converted.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryRewrite(root, diagnostic, almostExtension: false);

    /// <summary>Rewrites the containing class so the reported method lives in an extension block.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="almostExtension">Whether the receiver is an ordinary first parameter rather than a <c>this</c> one.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape cannot be converted.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic, bool almostExtension) =>
        root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>() is { } method
            && method.Parent is ClassDeclarationSyntax containingClass
            && Convert(containingClass, method, almostExtension) is { } updated
            ? new NodeReplacement(containingClass, updated, current => RewriteCurrentClass(current, method, almostExtension))
            : null;

    /// <summary>Redoes the conversion against the class as earlier edits in the same batch have left it.</summary>
    /// <param name="current">The containing class after the edits already composed.</param>
    /// <param name="method">The method as it stood in the original tree.</param>
    /// <param name="almostExtension">Whether the receiver is an ordinary first parameter rather than a <c>this</c> one.</param>
    /// <returns>The rewritten class, or <paramref name="current"/> when the method is no longer convertible.</returns>
    /// <remarks>
    /// Every conversion in a class replaces that whole class, so a batch holds several edits to one node. Each
    /// has to be derived again from what the previous ones produced — otherwise the second is computed against
    /// a class that no longer exists, and it neither sees the block the first opened nor keeps the order the
    /// members were declared in.
    /// </remarks>
    private static SyntaxNode RewriteCurrentClass(SyntaxNode current, MethodDeclarationSyntax method, bool almostExtension)
    {
        if (current is not ClassDeclarationSyntax containingClass)
        {
            return current;
        }

        foreach (var member in containingClass.Members)
        {
            if (member is MethodDeclarationSyntax candidate
                && candidate.IsEquivalentTo(method)
                && Convert(containingClass, candidate, almostExtension) is { } updated)
            {
                return updated;
            }
        }

        return current;
    }

    /// <summary>Moves one method into an extension block on its containing class.</summary>
    /// <param name="containingClass">The static class holding the extensions.</param>
    /// <param name="method">The method to convert.</param>
    /// <param name="almostExtension">Whether the receiver is an ordinary first parameter rather than a <c>this</c> one.</param>
    /// <returns>The rewritten class, or <see langword="null"/> when the shape cannot be converted.</returns>
    private static ClassDeclarationSyntax? Convert(ClassDeclarationSyntax containingClass, MethodDeclarationSyntax method, bool almostExtension)
    {
        if (!IsConvertible(method, almostExtension)
            || method.ParameterList.Parameters[0] is not { Type: { } receiverType } receiver
            || !TrySplitTypeParameters(method, receiverType, out var split))
        {
            return null;
        }

        var receiverName = receiver.Identifier.ValueText;
        var receiverModifiers = ReceiverModifierText(receiver.Modifiers);
        var member = ToExtensionMember(method, split).WithAdditionalAnnotations(Formatter.Annotation);
        if (FindMatchingBlock(containingClass, receiverType, receiverName, receiverModifiers, split) is { } existing)
        {
            // Only this move carries the member across the file, so what matters is the gap it travels and
            // whatever it takes with it. A directive in the method's own trivia is one half of a pair whose
            // other half stays behind, and a directive it has to cross is safe only when the whole region
            // crosses with it. A directive elsewhere among the members is in neither and does not count.
            return method.ContainsDirectives || DirectiveBoundaries.SeparateUnbalanced(method, existing)
                ? null
                : MergeIntoBlock(containingClass, existing, method, member);
        }

        if (ParseExtensionBlock(receiverType, receiverName, receiverModifiers, split) is not { } block)
        {
            return null;
        }

        var introduced = block.AddMembers(member)
            .WithLeadingTrivia(LayoutTriviaOf(method).AddRange(BlockDocumentation(receiverType, NewLineOf(containingClass))))
            .WithTrailingTrivia(method.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);
        return containingClass.ReplaceNode(method, introduced);
    }

    /// <summary>Builds the documentation the opened block carries.</summary>
    /// <param name="receiverType">The receiver type the block extends.</param>
    /// <param name="newLine">The line ending the document uses.</param>
    /// <returns>The documentation trivia that sits above the block.</returns>
    /// <remarks>
    /// The block is a declaration the fix introduces, and an undocumented one is what SST1654 reports. The
    /// receiver is named in a <c>c</c> element rather than a <c>cref</c> because a predefined alias such as
    /// <c>string</c> does not resolve as a cref (CS1574). The comment's terminator is part of the structured
    /// trivia rather than elastic whitespace, so the formatter never gets to normalise it and it has to be
    /// written as the document already writes its line endings.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SyntaxTriviaList BlockDocumentation(TypeSyntax receiverType, string newLine) =>
        SyntaxFactory.ParseLeadingTrivia(
            $"/// <summary>Extension members for <c>{EscapeXml(receiverType.WithoutTrivia().ToString())}</c>.</summary>{newLine}");

    /// <summary>Escapes the markup characters a type name can contain.</summary>
    /// <param name="text">The type name as it is written in source.</param>
    /// <returns>The name as XML character data.</returns>
    /// <remarks>
    /// A constructed generic receiver is written with angle brackets, which close the element around it and
    /// leave the comment malformed. The ampersand goes first so the entities this introduces are not escaped
    /// a second time.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string EscapeXml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>Gets the line ending a declaration is already written with.</summary>
    /// <param name="node">The declaration to read.</param>
    /// <returns>The first line ending found, or a bare line feed when there is none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string NewLineOf(SyntaxNode node) => LineEndingHelper.GetLineBreak(node).ToFullString();

    /// <summary>Gets a method's leading trivia without its documentation comment.</summary>
    /// <param name="method">The classic extension method being moved.</param>
    /// <returns>The blank lines and indentation that position the declaration.</returns>
    /// <remarks>
    /// The documentation travels with the member into the block. Leaving a copy on the block itself
    /// documents parameters the block does not declare (CS1572).
    /// </remarks>
    private static SyntaxTriviaList LayoutTriviaOf(MethodDeclarationSyntax method)
    {
        var leadingTrivia = method.GetLeadingTrivia();
        var kept = new List<SyntaxTrivia>(leadingTrivia.Count);
        foreach (var trivia in leadingTrivia)
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                kept.Add(trivia);
            }
        }

        return SyntaxFactory.TriviaList(kept);
    }

    /// <summary>Gets just the documentation comment from a declaration's leading trivia.</summary>
    /// <param name="node">The declaration to read.</param>
    /// <returns>The documentation trivia, or an empty list when the declaration has none.</returns>
    private static SyntaxTriviaList DocumentationOf(SyntaxNode node)
    {
        var leadingTrivia = node.GetLeadingTrivia();
        var kept = new List<SyntaxTrivia>(leadingTrivia.Count);
        foreach (var trivia in leadingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
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
    /// <returns>The rewritten class, or <see langword="null"/> when either node is no longer a member.</returns>
    /// <remarks>
    /// Both edits are made to the member list in one step. Doing them as two tree rewrites would leave the
    /// second one holding a node from a tree that no longer exists, and it also keeps the blank line that
    /// separated the two members visible after the first is gone.
    /// </remarks>
    private static ClassDeclarationSyntax? MergeIntoBlock(
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

        // The member keeps the position it held relative to the block, so a batch that converts the later
        // method first still leaves the block's members in the order they were declared.
        var updatedBlock = methodIndex < blockIndex
            ? block.WithMembers(block.Members.Insert(0, member))
            : block.AddMembers(member);

        // The first member carries the layout that follows the opening brace, so hand it to whichever
        // member inherits that position — without discarding the documentation the block already carries.
        if (methodIndex == 0 && blockIndex > methodIndex)
        {
            updatedBlock = updatedBlock.WithLeadingTrivia(LayoutTriviaOf(method).AddRange(DocumentationOf(block)));
        }

        var updated = members.Replace(block, updatedBlock).RemoveAt(methodIndex);
        return containingClass.WithMembers(updated);
    }

    /// <summary>Returns whether an extension method can be moved without further judgement.</summary>
    /// <param name="method">The method declaration.</param>
    /// <param name="almostExtension">Whether the receiver is an ordinary first parameter rather than a <c>this</c> one.</param>
    /// <returns><see langword="true"/> when the move is mechanical.</returns>
    /// <remarks>
    /// A receiver carrying attributes or a default has no equivalent on the block's parameter, so no fix
    /// is offered for one.
    /// </remarks>
    private static bool IsConvertible(MethodDeclarationSyntax method, bool almostExtension) =>
        (almostExtension
            ? Sst1709AlmostExtensionMethodAnalyzer.IsAlmostExtensionMethod(method)
            : ExtensionBlockHelper.IsClassicExtensionMethod(method))
        && method.ParameterList.Parameters[0] is { AttributeLists.Count: 0, Default: null, Type: not null }
        && (method.Body is not null || method.ExpressionBody is not null);

    /// <summary>Decides which of a generic method's type parameters the block declares and which the member keeps.</summary>
    /// <param name="method">The classic extension method being moved.</param>
    /// <param name="receiverType">The receiver parameter's type syntax.</param>
    /// <param name="split">The resulting division of type parameters and constraints.</param>
    /// <returns><see langword="true"/> when the division can be written.</returns>
    /// <remarks>
    /// The block declares whatever the receiver type names, because those are inferred from the receiver
    /// at the call site exactly as the method inferred them; everything else stays on the member, where it
    /// is still inferred from the arguments.
    /// </remarks>
    private static bool TrySplitTypeParameters(MethodDeclarationSyntax method, TypeSyntax receiverType, out TypeParameterSplit split)
    {
        split = new(null, default, method.TypeParameterList, method.ConstraintClauses);
        if (method.TypeParameterList is not { } declared)
        {
            return true;
        }

        var onBlock = new List<TypeParameterSyntax>(declared.Parameters.Count);
        var onMember = new List<TypeParameterSyntax>(declared.Parameters.Count);
        SplitParameters(declared, receiverType, onBlock, onMember);

        var blockClauses = new List<TypeParameterConstraintClauseSyntax>(method.ConstraintClauses.Count);
        var memberClauses = new List<TypeParameterConstraintClauseSyntax>(method.ConstraintClauses.Count);
        if (!TrySplitConstraints(method.ConstraintClauses, onBlock, onMember, blockClauses, memberClauses))
        {
            return false;
        }

        split = new(
            onBlock.Count == 0 ? null : SyntaxFactory.TypeParameterList(SyntaxFactory.SeparatedList(onBlock)),
            SyntaxFactory.List(blockClauses),
            onMember.Count == 0 ? null : declared.WithParameters(SyntaxFactory.SeparatedList(onMember)),
            SyntaxFactory.List(memberClauses));
        return true;
    }

    /// <summary>Sorts declared type parameters into the ones the receiver names and the ones it does not.</summary>
    /// <param name="declared">The method's type parameter list.</param>
    /// <param name="receiverType">The receiver parameter's type syntax.</param>
    /// <param name="onBlock">Receives the type parameters the receiver names.</param>
    /// <param name="onMember">Receives the rest.</param>
    private static void SplitParameters(
        TypeParameterListSyntax declared,
        TypeSyntax receiverType,
        List<TypeParameterSyntax> onBlock,
        List<TypeParameterSyntax> onMember)
    {
        var parameters = declared.Parameters;
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            var destination = MentionsName(receiverType, parameter.Identifier.ValueText) ? onBlock : onMember;
            destination.Add(parameter);
        }
    }

    /// <summary>Sends each constraint clause to whichever side declares the type parameter it narrows.</summary>
    /// <param name="clauses">The method's constraint clauses.</param>
    /// <param name="onBlock">The type parameters the block declares.</param>
    /// <param name="onMember">The type parameters the member keeps.</param>
    /// <param name="blockClauses">Receives the block's clauses.</param>
    /// <param name="memberClauses">Receives the member's clauses.</param>
    /// <returns><see langword="true"/> when every clause can be written where its type parameter lives.</returns>
    /// <remarks>
    /// A member's type parameters are not in scope on the block, so a block clause that names one cannot be
    /// written on either side and the whole move is declined.
    /// </remarks>
    private static bool TrySplitConstraints(
        SyntaxList<TypeParameterConstraintClauseSyntax> clauses,
        List<TypeParameterSyntax> onBlock,
        List<TypeParameterSyntax> onMember,
        List<TypeParameterConstraintClauseSyntax> blockClauses,
        List<TypeParameterConstraintClauseSyntax> memberClauses)
    {
        for (var index = 0; index < clauses.Count; index++)
        {
            var clause = clauses[index];
            if (!Declares(onBlock, clause.Name.Identifier.ValueText))
            {
                memberClauses.Add(clause);
                continue;
            }

            if (MentionsAny(clause, onMember))
            {
                return false;
            }

            blockClauses.Add(clause);
        }

        return true;
    }

    /// <summary>Returns whether a type parameter list declares a name.</summary>
    /// <param name="parameters">The type parameters to search.</param>
    /// <param name="name">The name to find.</param>
    /// <returns><see langword="true"/> when one of them carries the name.</returns>
    private static bool Declares(List<TypeParameterSyntax> parameters, string name)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            if (parameters[index].Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a node names any of the given type parameters.</summary>
    /// <param name="node">The node to search.</param>
    /// <param name="parameters">The type parameters to look for.</param>
    /// <returns><see langword="true"/> when the node names one of them.</returns>
    private static bool MentionsAny(SyntaxNode node, List<TypeParameterSyntax> parameters)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            if (MentionsName(node, parameters[index].Identifier.ValueText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a node names an identifier anywhere within it.</summary>
    /// <param name="node">The node to search.</param>
    /// <param name="name">The identifier to find.</param>
    /// <returns><see langword="true"/> when the identifier appears.</returns>
    private static bool MentionsName(SyntaxNode node, string name)
    {
        if (node is IdentifierNameSyntax identifier)
        {
            return identifier.Identifier.ValueText == name;
        }

        foreach (var child in node.ChildNodes())
        {
            if (MentionsName(child, name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Renders a type parameter list as the text a parsed block declares.</summary>
    /// <param name="list">The type parameter list, or <see langword="null"/>.</param>
    /// <returns>The rendered list, or an empty string when there is none.</returns>
    private static string RenderTypeParameters(TypeParameterListSyntax? list)
    {
        if (list is not { Parameters.Count: > 0 } parameters)
        {
            return string.Empty;
        }

        var rendered = new StringBuilder("<", parameters.Span.Length + parameters.Parameters.Count);
        for (var index = 0; index < parameters.Parameters.Count; index++)
        {
            if (index > 0)
            {
                _ = rendered.Append(", ");
            }

            _ = rendered.Append(parameters.Parameters[index].WithoutTrivia().ToString());
        }

        return rendered.Append('>').ToString();
    }

    /// <summary>Renders constraint clauses as the text a parsed block declares, one per line.</summary>
    /// <param name="clauses">The constraint clauses.</param>
    /// <returns>The rendered clauses, or an empty string when there are none.</returns>
    private static string RenderConstraints(SyntaxList<TypeParameterConstraintClauseSyntax> clauses)
    {
        if (clauses.Count == 0)
        {
            return string.Empty;
        }

        var capacity = clauses.Count;
        for (var index = 0; index < clauses.Count; index++)
        {
            capacity += clauses[index].Span.Length;
        }

        var rendered = new StringBuilder(capacity);
        for (var index = 0; index < clauses.Count; index++)
        {
            _ = rendered.Append('\n').Append(clauses[index].NormalizeWhitespace().ToString());
        }

        return rendered.ToString();
    }

    /// <summary>Returns the extension block in the class that already declares this receiver, if any.</summary>
    /// <param name="containingClass">The static class holding the extensions.</param>
    /// <param name="receiverType">The receiver type of the method being moved.</param>
    /// <param name="receiverName">The receiver parameter name the method's body refers to.</param>
    /// <param name="receiverModifiers">How the method takes its receiver, without <c>this</c>.</param>
    /// <param name="split">The division of type parameters and constraints the move produces.</param>
    /// <returns>The matching block, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The name has to match as well as the type: the moved body refers to the receiver by the name the
    /// method gave it, and a block declaring the same type under a different name would not compile. How
    /// the receiver is passed has to match too — a by-value block cannot host a method that took its
    /// receiver by readonly reference — as do the block's own type parameters and their constraints.
    /// </remarks>
    private static TypeDeclarationSyntax? FindMatchingBlock(
        ClassDeclarationSyntax containingClass,
        TypeSyntax receiverType,
        string receiverName,
        string receiverModifiers,
        in TypeParameterSplit split)
    {
        var receiverText = ExtensionBlockHelper.ReceiverTypeText(receiverType);
        foreach (var member in containingClass.Members)
        {
            if (member is TypeDeclarationSyntax block
                && ExtensionBlockHelper.IsExtensionBlock(block)
                && BlockMatches(block, receiverText, receiverName, receiverModifiers, split))
            {
                return block;
            }
        }

        return null;
    }

    /// <summary>Returns whether an existing block declares exactly what the moved method needs.</summary>
    /// <param name="block">The candidate extension block.</param>
    /// <param name="receiverText">The receiver type text of the method being moved.</param>
    /// <param name="receiverName">The receiver parameter name the method's body refers to.</param>
    /// <param name="receiverModifiers">How the method takes its receiver, without <c>this</c>.</param>
    /// <param name="split">The division of type parameters and constraints the move produces.</param>
    /// <returns><see langword="true"/> when the member can be added to the block as written.</returns>
    private static bool BlockMatches(
        TypeDeclarationSyntax block,
        string? receiverText,
        string receiverName,
        string receiverModifiers,
        in TypeParameterSplit split) =>
        block.ParameterList?.Parameters is { Count: 1 } parameters
            && parameters[0].Identifier.ValueText == receiverName
            && ReceiverModifierText(parameters[0].Modifiers) == receiverModifiers
            && ExtensionBlockHelper.ReceiverTypeText(block) == receiverText
            && RenderTypeParameters(block.TypeParameterList) == RenderTypeParameters(split.BlockTypeParameters)
            && RenderConstraints(block.ConstraintClauses) == RenderConstraints(split.BlockConstraints);

    /// <summary>Renders how a receiver is passed, leaving out the <c>this</c> that marks the method.</summary>
    /// <param name="modifiers">The receiver parameter's modifiers.</param>
    /// <returns>The remaining modifiers in source order, space separated.</returns>
    /// <remarks>
    /// <c>this in T</c> passes a large readonly struct by readonly reference, and dropping the <c>in</c>
    /// would copy it at every call. The block's parameter carries the same modifiers the method did.
    /// </remarks>
    private static string ReceiverModifierText(in SyntaxTokenList modifiers)
    {
        var rendered = new StringBuilder(modifiers.Span.Length);
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.ThisKeyword))
            {
                continue;
            }

            if (rendered.Length > 0)
            {
                _ = rendered.Append(' ');
            }

            _ = rendered.Append(modifiers[i].ValueText);
        }

        return rendered.ToString();
    }

    /// <summary>Parses an empty extension block for a receiver.</summary>
    /// <param name="receiverType">The receiver type.</param>
    /// <param name="receiverName">The receiver parameter name.</param>
    /// <param name="receiverModifiers">How the method takes its receiver, without <c>this</c>.</param>
    /// <param name="split">The division of type parameters and constraints the move produces.</param>
    /// <returns>The parsed block, or <see langword="null"/> when the host parser does not accept it.</returns>
    /// <remarks>
    /// A modifier the language does not allow on a block's receiver comes back from the parser as a
    /// diagnostic, which declines the fix rather than writing something that will not compile. The
    /// constraints are attached afterwards rather than written into the text, so that the formatter rather
    /// than this decides where they sit: a line break written into the text it leaves at column zero.
    /// </remarks>
    private static TypeDeclarationSyntax? ParseExtensionBlock(
        TypeSyntax receiverType,
        string receiverName,
        string receiverModifiers,
        in TypeParameterSplit split)
    {
        var prefix = receiverModifiers.Length == 0 ? string.Empty : $"{receiverModifiers} ";
        var typeParameters = RenderTypeParameters(split.BlockTypeParameters);
        var parsed = SyntaxFactory.ParseMemberDeclaration(
            $"extension{typeParameters}({prefix}{receiverType} {receiverName})\n{{\n}}\n");
        if (parsed is not TypeDeclarationSyntax block
            || !ExtensionBlockHelper.IsExtensionBlock(block)
            || parsed.ContainsDiagnostics)
        {
            return null;
        }

        return split.BlockConstraints.Count == 0
            ? block
            : block
                .WithParameterList(block.ParameterList!.WithCloseParenToken(block.ParameterList.CloseParenToken.WithTrailingTrivia(SyntaxFactory.ElasticMarker)))
                .WithConstraintClauses(Elastic(split.BlockConstraints));
    }

    /// <summary>Hands constraint clauses to the formatter to place.</summary>
    /// <param name="clauses">The constraint clauses.</param>
    /// <returns>The clauses with their surrounding whitespace left elastic.</returns>
    private static SyntaxList<TypeParameterConstraintClauseSyntax> Elastic(SyntaxList<TypeParameterConstraintClauseSyntax> clauses)
    {
        var placed = new List<TypeParameterConstraintClauseSyntax>(clauses.Count);
        for (var index = 0; index < clauses.Count; index++)
        {
            placed.Add(clauses[index]
                .NormalizeWhitespace()
                .WithLeadingTrivia(SyntaxFactory.ElasticSpace)
                .WithTrailingTrivia(SyntaxFactory.ElasticMarker));
        }

        return SyntaxFactory.List(placed);
    }

    /// <summary>Rewrites a classic extension method as an extension-block member.</summary>
    /// <param name="method">The method declaration.</param>
    /// <param name="split">The division of type parameters and constraints the move produces.</param>
    /// <returns>The member as it is declared inside the block.</returns>
    /// <remarks>
    /// Inside a block the receiver is the block's parameter, so the method drops its own receiver
    /// parameter, its <c>static</c> modifier, and whatever the block now declares; everything else —
    /// attributes, the rest of the documentation, the body — moves across untouched.
    /// </remarks>
    private static MethodDeclarationSyntax ToExtensionMember(MethodDeclarationSyntax method, in TypeParameterSplit split)
    {
        var receiverName = method.ParameterList.Parameters[0].Identifier.ValueText;
        var parameterList = method.ParameterList.WithParameters(method.ParameterList.Parameters.RemoveAt(0));

        // The closing parenthesis holds the line break that introduced the constraints. When they all
        // move to the block it would push the body onto a line of its own, so it goes with them.
        if (split.MemberConstraints.Count == 0 && method.ConstraintClauses.Count > 0)
        {
            parameterList = parameterList.WithCloseParenToken(parameterList.CloseParenToken.WithTrailingTrivia(SyntaxFactory.ElasticMarker));
        }

        // The trivia goes on last: replacing the modifiers restores the tokens' own leading trivia,
        // which still carries the documentation this strips.
        return method.Update(
                method.AttributeLists,
                WithoutStatic(method.Modifiers),
                method.ReturnType,
                method.ExplicitInterfaceSpecifier,
                method.Identifier,
                split.MemberTypeParameters,
                parameterList,
                split.MemberConstraints,
                method.Body,
                method.ExpressionBody,
                method.SemicolonToken)
            .WithLeadingTrivia(WithoutMovedDocumentation(method.GetLeadingTrivia(), receiverName, split));
    }

    /// <summary>Removes the documentation for everything the block now declares.</summary>
    /// <param name="trivia">The declaration's leading trivia.</param>
    /// <param name="receiverName">The receiver parameter's name.</param>
    /// <param name="split">The division of type parameters and constraints the move produces.</param>
    /// <returns>The trivia with those elements removed.</returns>
    /// <remarks>
    /// The receiver and the block's type parameters belong to the block, so documenting them on the member
    /// describes what the member no longer declares (CS1572, CS1711).
    /// </remarks>
    private static SyntaxTriviaList WithoutMovedDocumentation(in SyntaxTriviaList trivia, string receiverName, in TypeParameterSplit split)
    {
        var blockTypeParameters = split.BlockTypeParameters;
        for (var i = 0; i < trivia.Count; i++)
        {
            if (trivia[i].GetStructure() is not DocumentationCommentTriviaSyntax documentation)
            {
                continue;
            }

            var kept = documentation.Content;
            for (var j = kept.Count - 1; j >= 0; j--)
            {
                if (DocumentsMovedName(kept[j], receiverName, blockTypeParameters))
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

    /// <summary>Returns whether a documentation element describes something the block now declares.</summary>
    /// <param name="node">The documentation node.</param>
    /// <param name="receiverName">The receiver parameter's name.</param>
    /// <param name="blockTypeParameters">The type parameters the block declares.</param>
    /// <returns><see langword="true"/> when the element belongs with the block rather than the member.</returns>
    private static bool DocumentsMovedName(XmlNodeSyntax node, string receiverName, TypeParameterListSyntax? blockTypeParameters)
    {
        if (node is not XmlElementSyntax element)
        {
            return false;
        }

        return element.StartTag.Name.LocalName.ValueText switch
        {
            "param" => NamesParameter(element, receiverName),
            "typeparam" => NamesBlockTypeParameter(element, blockTypeParameters),
            _ => false
        };
    }

    /// <summary>Returns whether a <c>&lt;typeparam&gt;</c> element names one of the block's type parameters.</summary>
    /// <param name="element">The documentation element.</param>
    /// <param name="blockTypeParameters">The type parameters the block declares.</param>
    /// <returns><see langword="true"/> when the element documents one of them.</returns>
    private static bool NamesBlockTypeParameter(XmlElementSyntax element, TypeParameterListSyntax? blockTypeParameters)
    {
        if (blockTypeParameters is null)
        {
            return false;
        }

        var parameters = blockTypeParameters.Parameters;
        for (var index = 0; index < parameters.Count; index++)
        {
            if (NamesParameter(element, parameters[index].Identifier.ValueText))
            {
                return true;
            }
        }

        return false;
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

    /// <summary>How a moved method's type parameters and constraints divide between the block and the member.</summary>
    /// <param name="BlockTypeParameters">The type parameters the block declares, or <see langword="null"/>.</param>
    /// <param name="BlockConstraints">The constraints on the block's type parameters.</param>
    /// <param name="MemberTypeParameters">The type parameters the member keeps, or <see langword="null"/>.</param>
    /// <param name="MemberConstraints">The constraints on the member's type parameters.</param>
    private readonly record struct TypeParameterSplit(
        TypeParameterListSyntax? BlockTypeParameters,
        SyntaxList<TypeParameterConstraintClauseSyntax> BlockConstraints,
        TypeParameterListSyntax? MemberTypeParameters,
        SyntaxList<TypeParameterConstraintClauseSyntax> MemberConstraints);
}
