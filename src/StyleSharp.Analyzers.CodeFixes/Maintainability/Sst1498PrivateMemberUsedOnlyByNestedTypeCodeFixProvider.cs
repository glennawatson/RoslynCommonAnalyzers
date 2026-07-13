// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Moves a private static method that only a nested type calls into that nested type (SST1498). The method
/// moves as a whole node, so its documentation comment and the comments around it travel with it, and the
/// formatter re-indents it for its new home.
/// </summary>
/// <remarks>
/// <para>
/// The fix is offered only where the move cannot change what the code means. The C# rule that makes it safe
/// is that a nested type still sees everything its enclosing type declares, so the moved method's body keeps
/// binding to the outer type's other private members exactly as it did before; and a call written as a bare
/// <c>Helper(…)</c> inside the nested type keeps binding, now to the method in its own type. So the fix
/// requires all of:
/// </para>
/// <list type="bullet">
/// <item><description>
/// A <c>static</c> method. An instance member's state belongs to an instance of the outer type, and the
/// nested type has no implicit access to one — moving it would mean deciding whose state it now is, which is
/// a design question. A field's move would also change <em>when</em> its initializer runs, because static
/// initialization is per type.
/// </description></item>
/// <item><description>
/// Every nested use written unqualified. A call spelled <c>Outer.Helper()</c> names the type the method is
/// leaving, and would be left pointing at nothing.
/// </description></item>
/// <item><description>
/// No other member of that name in the outer type, and none in the nested type. C# name lookup stops at the
/// first type that declares the name, so moving one overload of a family into the nested type would hide the
/// rest of the family from every unqualified call inside it.
/// </description></item>
/// <item><description>
/// A nested class, struct or record — declared whole, in one part — and a type free of preprocessor
/// directives, which a moved member would carry with it and unbalance.
/// </description></item>
/// </list>
/// <para>
/// Every other reported member is left to a human: the diagnostic points at the member, and the move is
/// theirs to make.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1498PrivateMemberUsedOnlyByNestedTypeCodeFixProvider))]
[Shared]
public sealed class Sst1498PrivateMemberUsedOnlyByNestedTypeCodeFixProvider : CodeFixProvider, IAsyncBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.PrivateMemberUsedOnlyByNestedType.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => AsyncBatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var move = await FindMoveAsync(context.Document, root, diagnostic, context.CancellationToken).ConfigureAwait(false);
            if (move is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Move the method into the nested type",
                    _ => Task.FromResult(Apply(context.Document, root, move)),
                    equivalenceKey: nameof(Sst1498PrivateMemberUsedOnlyByNestedTypeCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    async Task IAsyncBatchableCodeFix.RegisterEditsAsync(DocumentEditor editor, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var move = await FindMoveAsync(editor.OriginalDocument, editor.OriginalRoot, diagnostic, cancellationToken).ConfigureAwait(false);
        if (move is null)
        {
            return;
        }

        var moved = ForNestedType(move.Method, move.NestedType);
        editor.RemoveNode(move.Method);
        editor.ReplaceNode(move.NestedType, (current, _) => Append(current, moved));

        if (ClosesTheGap(move) is { } follower)
        {
            editor.ReplaceNode(follower, (current, _) => WithoutLeadingBlankLine((MemberDeclarationSyntax)current));
        }
    }

    /// <summary>Applies one move to the document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="move">The move to apply.</param>
    /// <returns>The updated document.</returns>
    private static Document Apply(Document document, SyntaxNode root, MemberMove move)
    {
        // The list is rebuilt in one pass over the original members. Chaining the edits instead would not
        // work: the list a Replace hands back is detached, so the nodes read out of it are new objects, and a
        // following Remove of the original method node would match nothing.
        var original = move.OuterType.Members;
        var follower = ClosesTheGap(move);
        var members = new List<MemberDeclarationSyntax>(original.Count);
        for (var i = 0; i < original.Count; i++)
        {
            var member = original[i];
            if (member == move.Method)
            {
                continue;
            }

            // The nested type is usually the member that also closes the gap, so whether this member is the
            // follower is decided before the append replaces the node it would be compared against.
            var closesTheGap = member == follower;
            if (member == move.NestedType)
            {
                member = (MemberDeclarationSyntax)Append(move.NestedType, ForNestedType(move.Method, move.NestedType));
            }

            members.Add(closesTheGap ? WithoutLeadingBlankLine(member) : member);
        }

        var outer = move.OuterType.WithMembers(SyntaxFactory.List(members));
        return document.WithSyntaxRoot(root.ReplaceNode(move.OuterType, outer));
    }

    /// <summary>Gets the member that has to lose a blank line because it inherits the first slot.</summary>
    /// <param name="move">The move being applied.</param>
    /// <returns>The member that becomes the type's first, or <see langword="null"/> when none does.</returns>
    /// <remarks>
    /// The first member of a type carries no blank line of its own — the opening brace's newline is all that
    /// precedes it. Every later member carries one. So when the method leaving the type was its first, the
    /// member behind it inherits the slot while still carrying the blank line that used to separate the two,
    /// and the type would open on an empty line.
    /// </remarks>
    private static MemberDeclarationSyntax? ClosesTheGap(MemberMove move)
    {
        var members = move.OuterType.Members;
        return members.Count > 1 && members[0] == move.Method ? members[1] : null;
    }

    /// <summary>Drops the blank line a member carries above it, keeping any comment it also carries.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The member, without the separating newline.</returns>
    private static MemberDeclarationSyntax WithoutLeadingBlankLine(MemberDeclarationSyntax member)
    {
        var leading = member.GetLeadingTrivia();
        for (var i = 0; i < leading.Count; i++)
        {
            if (leading[i].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return member.WithLeadingTrivia(leading.RemoveAt(i));
            }

            if (!leading[i].IsKind(SyntaxKind.WhitespaceTrivia))
            {
                // A comment or a documentation comment comes first: it owns the space above the member.
                break;
            }
        }

        return member;
    }

    /// <summary>Appends the moved method to a nested type's members.</summary>
    /// <param name="node">The current nested type, including any nested batch edits.</param>
    /// <param name="method">The method to append.</param>
    /// <returns>The nested type with the method added.</returns>
    private static SyntaxNode Append(SyntaxNode node, MethodDeclarationSyntax method)
        => node is TypeDeclarationSyntax type ? type.WithMembers(type.Members.Add(method)) : node;

    /// <summary>Prepares the method for its new home, one level deeper in the file.</summary>
    /// <param name="method">The method being moved.</param>
    /// <param name="nested">The nested type it is moving into.</param>
    /// <returns>The method, separated from what it now follows and marked for the formatter to re-indent.</returns>
    /// <remarks>
    /// The whitespace the method carried is dropped and one elastic newline put in its place: elastic trivia
    /// is what the formatter is allowed to rewrite, so the blank line comes out in the file's own line ending
    /// and the method — with its documentation comment — is re-indented for the type it now sits in. Anything
    /// that is not whitespace is kept exactly as written.
    /// </remarks>
    private static MethodDeclarationSyntax ForNestedType(MethodDeclarationSyntax method, TypeDeclarationSyntax nested)
    {
        var leading = method.GetLeadingTrivia();
        var start = 0;
        while (start < leading.Count && IsLayout(leading[start]))
        {
            start++;
        }

        var kept = leading.Count == start ? SyntaxFactory.TriviaList() : SyntaxFactory.TriviaList(GetRange(leading, start));
        var separated = nested.Members.Count == 0
            ? kept
            : SyntaxFactory.TriviaList(SyntaxFactory.ElasticCarriageReturnLineFeed).AddRange(kept);

        return method.WithLeadingTrivia(separated).WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>Returns whether a trivia is only layout — whitespace or a line break.</summary>
    /// <param name="trivia">The trivia to classify.</param>
    /// <returns><see langword="true"/> when it carries nothing the author wrote.</returns>
    private static bool IsLayout(SyntaxTrivia trivia)
        => trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia);

    /// <summary>Gets the trivia from an index onwards.</summary>
    /// <param name="trivia">The trivia list.</param>
    /// <param name="start">The first index to keep.</param>
    /// <returns>The kept trivia.</returns>
    private static List<SyntaxTrivia> GetRange(SyntaxTriviaList trivia, int start)
    {
        var kept = new List<SyntaxTrivia>(trivia.Count - start);
        for (var i = start; i < trivia.Count; i++)
        {
            kept.Add(trivia[i]);
        }

        return kept;
    }

    /// <summary>Resolves a diagnostic to a move that cannot change what the code means.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The move, or <see langword="null"/> when this member is not one to move mechanically.</returns>
    private static async Task<MemberMove?> FindMoveAsync(
        Document document,
        SyntaxNode root,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not MethodDeclarationSyntax method
            || !ModifierListHelper.Contains(method.Modifiers, SyntaxKind.StaticKeyword)
            || method.Parent is not TypeDeclarationSyntax outer
            || outer.ContainsDirectives)
        {
            return null;
        }

        if (await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false) is not { } model
            || NestedTypeOnlyMembers.Collect(model, outer, cancellationToken) is not { } reported)
        {
            return null;
        }

        return FindMember(reported, method) is { NestedUsesAreUnqualified: true, NestedUser: TypeDeclarationSyntax nested }
            && CanHost(model, nested, outer, method, cancellationToken)
                ? new MemberMove(outer, nested, method)
                : null;
    }

    /// <summary>Returns whether the nested type can host the method without changing what any name means.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="nested">The nested type the method would move into.</param>
    /// <param name="outer">The type the method would leave.</param>
    /// <param name="method">The method being moved.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the move is a pure relocation.</returns>
    private static bool CanHost(
        SemanticModel model,
        TypeDeclarationSyntax nested,
        TypeDeclarationSyntax outer,
        MethodDeclarationSyntax method,
        CancellationToken cancellationToken)
    {
        var name = method.Identifier.ValueText;
        return nested is not InterfaceDeclarationSyntax
            && !ModifierListHelper.Contains(nested.Modifiers, SyntaxKind.PartialKeyword)
            && CountMembersNamed(outer, name) == 1
            && CountMembersNamed(nested, name) == 0
            && HasNoBaseType(model, nested, cancellationToken);
    }

    /// <summary>Returns whether a nested type inherits nothing that the moved method could collide with.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="nested">The nested type the method would move into.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the type derives straight from <c>object</c> (or is a struct).</returns>
    /// <remarks>
    /// A member declared in a type hides an inherited one of the same name — and hides the whole inherited
    /// family from every unqualified call in that type. Counting the names the nested type writes down is
    /// therefore not enough: a base class it never mentions could be where that name lives today.
    /// </remarks>
    private static bool HasNoBaseType(SemanticModel model, TypeDeclarationSyntax nested, CancellationToken cancellationToken)
        => model.GetDeclaredSymbol(nested, cancellationToken) is { } symbol
            && symbol.BaseType is null or { SpecialType: SpecialType.System_Object or SpecialType.System_ValueType };

    /// <summary>Finds the reported member that matches one declaration.</summary>
    /// <param name="reported">The members the rule reported in this type.</param>
    /// <param name="method">The declaration the diagnostic named.</param>
    /// <returns>The reported member, or <see langword="null"/> when the code has since changed.</returns>
    private static NestedTypeOnlyMember? FindMember(List<NestedTypeOnlyMember> reported, MethodDeclarationSyntax method)
    {
        for (var i = 0; i < reported.Count; i++)
        {
            if (reported[i].Declaration == method)
            {
                return reported[i];
            }
        }

        return null;
    }

    /// <summary>Counts the members of a type that declare one name.</summary>
    /// <param name="type">The type declaration.</param>
    /// <param name="name">The member name to count.</param>
    /// <returns>The number of members declaring that name.</returns>
    /// <remarks>
    /// Fields are counted by their variables, so a field named like the method still blocks the move — the
    /// point is that the name must be free in the nested type and alone in the outer one.
    /// </remarks>
    private static int CountMembersNamed(TypeDeclarationSyntax type, string name)
    {
        var count = 0;
        var members = type.Members;
        for (var i = 0; i < members.Count; i++)
        {
            count += CountDeclarationsNamed(members[i], name);
        }

        return count;
    }

    /// <summary>Counts the declarations one member makes under a name.</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="name">The member name to count.</param>
    /// <returns>The number of declarations of that name.</returns>
    private static int CountDeclarationsNamed(MemberDeclarationSyntax member, string name)
    {
        switch (member)
        {
            case FieldDeclarationSyntax field:
            {
                var count = 0;
                var variables = field.Declaration.Variables;
                for (var i = 0; i < variables.Count; i++)
                {
                    count += string.Equals(variables[i].Identifier.ValueText, name, StringComparison.Ordinal) ? 1 : 0;
                }

                return count;
            }

            case MethodDeclarationSyntax method:
            {
                return string.Equals(method.Identifier.ValueText, name, StringComparison.Ordinal) ? 1 : 0;
            }

            case PropertyDeclarationSyntax property:
            {
                return string.Equals(property.Identifier.ValueText, name, StringComparison.Ordinal) ? 1 : 0;
            }

            case BaseTypeDeclarationSyntax type:
            {
                return string.Equals(type.Identifier.ValueText, name, StringComparison.Ordinal) ? 1 : 0;
            }

            default:
            {
                return 0;
            }
        }
    }

    /// <summary>One method, the type it leaves, and the nested type it moves into.</summary>
    private sealed class MemberMove
    {
        /// <summary>Initializes a new instance of the <see cref="MemberMove"/> class.</summary>
        /// <param name="outerType">The type the method leaves.</param>
        /// <param name="nestedType">The nested type the method moves into.</param>
        /// <param name="method">The method being moved.</param>
        public MemberMove(TypeDeclarationSyntax outerType, TypeDeclarationSyntax nestedType, MethodDeclarationSyntax method)
        {
            OuterType = outerType;
            NestedType = nestedType;
            Method = method;
        }

        /// <summary>Gets the type the method leaves.</summary>
        public TypeDeclarationSyntax OuterType { get; }

        /// <summary>Gets the nested type the method moves into.</summary>
        public TypeDeclarationSyntax NestedType { get; }

        /// <summary>Gets the method being moved.</summary>
        public MethodDeclarationSyntax Method { get; }
    }
}
