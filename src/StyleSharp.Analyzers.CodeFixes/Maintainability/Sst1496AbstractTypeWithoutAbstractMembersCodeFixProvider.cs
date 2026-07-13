// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.FindSymbols;

namespace StyleSharp.Analyzers;

/// <summary>
/// Turns an abstract class that asks nothing of its derived types into a concrete one (SST1496): the
/// <c>abstract</c> modifier is removed, and replaced by <c>sealed</c> when nothing derives from the type and
/// nothing in it was written for a derived type to reach.
/// </summary>
/// <remarks>
/// <para>
/// Sealing is offered only when it is provably safe. A class something already derives from cannot be
/// sealed, so the whole solution is searched for a derived class first. A class holding a <c>protected</c>
/// member or a <c>virtual</c> member was written to be extended, and sealing it would leave a member the
/// language then complains about — so those keep only the modifier removal, which is the half of the fix
/// that is always correct.
/// </para>
/// <para>
/// A <c>partial</c> class is skipped entirely. Its modifiers are spread across parts this fix cannot see all
/// of, and guessing which part should carry <c>sealed</c> is exactly the kind of guess a fix should not
/// make.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1496AbstractTypeWithoutAbstractMembersCodeFixProvider))]
[Shared]
public sealed class Sst1496AbstractTypeWithoutAbstractMembersCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(MaintainabilityRules.AbstractTypeWithoutAbstractMembers.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
            if (FindClass(root, diagnostic) is not { } declaration
                || ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.PartialKeyword)
                || IndexOfAbstract(declaration.Modifiers) < 0)
            {
                continue;
            }

            var seal = await CanSealAsync(context.Document, declaration, context.CancellationToken).ConfigureAwait(false);
            context.RegisterCodeFix(
                CodeAction.Create(
                    seal ? "Seal the type instead of making it abstract" : "Remove the 'abstract' modifier",
                    _ => Task.FromResult(Apply(context.Document, root, declaration, seal)),
                    equivalenceKey: nameof(Sst1496AbstractTypeWithoutAbstractMembersCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Rewrites the class as a concrete — and, where safe, sealed — type.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="declaration">The reported class declaration.</param>
    /// <param name="seal">Whether <c>abstract</c> is replaced by <c>sealed</c> rather than simply dropped.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ClassDeclarationSyntax declaration, bool seal)
    {
        var index = IndexOfAbstract(declaration.Modifiers);
        if (index < 0)
        {
            return document;
        }

        var updated = seal ? Seal(declaration, index) : DropAbstract(declaration, index);
        return document.WithSyntaxRoot(root.ReplaceNode(declaration, updated));
    }

    /// <summary>Resolves the diagnostic's span back to the class it reported.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The class declaration, or <see langword="null"/>.</returns>
    private static ClassDeclarationSyntax? FindClass(SyntaxNode root, Diagnostic diagnostic)
        => root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<ClassDeclarationSyntax>();

    /// <summary>Gets the position of the <c>abstract</c> modifier in a modifier list.</summary>
    /// <param name="modifiers">The class's modifiers.</param>
    /// <returns>The index, or -1 when the modifier is absent.</returns>
    private static int IndexOfAbstract(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.AbstractKeyword))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns whether the class can be sealed without breaking anything.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="declaration">The reported class declaration.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when nothing derives from the class and nothing in it expects to be inherited.</returns>
    private static async Task<bool> CanSealAsync(
        Document document,
        ClassDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        if (DeclaresInheritanceOnlyMember(declaration))
        {
            return false;
        }

        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (model?.GetDeclaredSymbol(declaration, cancellationToken) is not { } type)
        {
            return false;
        }

        var derived = await SymbolFinder
            .FindDerivedClassesAsync(type, document.Project.Solution, projects: null, cancellationToken)
            .ConfigureAwait(false);
        using var candidates = derived.GetEnumerator();
        return !candidates.MoveNext();
    }

    /// <summary>Returns whether the class declares a member that only a derived type could use.</summary>
    /// <param name="declaration">The reported class declaration.</param>
    /// <returns><see langword="true"/> for a <c>protected</c> or <c>virtual</c> member, which a sealed type may not hold.</returns>
    private static bool DeclaresInheritanceOnlyMember(ClassDeclarationSyntax declaration)
    {
        var members = declaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            var modifiers = members[i].Modifiers;
            if (ModifierListHelper.ContainsEither(modifiers, SyntaxKind.ProtectedKeyword, SyntaxKind.VirtualKeyword))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Replaces the <c>abstract</c> modifier with <c>sealed</c>, keeping its trivia.</summary>
    /// <param name="declaration">The reported class declaration.</param>
    /// <param name="index">The position of the <c>abstract</c> modifier.</param>
    /// <returns>The sealed class declaration.</returns>
    private static ClassDeclarationSyntax Seal(ClassDeclarationSyntax declaration, int index)
    {
        var modifier = declaration.Modifiers[index];
        var sealedKeyword = SyntaxFactory.Token(SyntaxKind.SealedKeyword).WithTriviaFrom(modifier);
        return declaration.WithModifiers(declaration.Modifiers.Replace(modifier, sealedKeyword));
    }

    /// <summary>Removes the <c>abstract</c> modifier, moving its leading trivia onto whatever now comes first.</summary>
    /// <param name="declaration">The reported class declaration.</param>
    /// <param name="index">The position of the <c>abstract</c> modifier.</param>
    /// <returns>The concrete class declaration.</returns>
    /// <remarks>
    /// The first modifier of a declaration carries its documentation comment, its attributes' trailing
    /// newline and its indentation, so removing it without moving that trivia along would take the
    /// documentation with it.
    /// </remarks>
    private static ClassDeclarationSyntax DropAbstract(ClassDeclarationSyntax declaration, int index)
    {
        var modifier = declaration.Modifiers[index];
        var modifiers = declaration.Modifiers.RemoveAt(index);
        if (index > 0)
        {
            return declaration.WithModifiers(modifiers);
        }

        if (modifiers.Count > 0)
        {
            return declaration.WithModifiers(modifiers.Replace(modifiers[0], modifiers[0].WithLeadingTrivia(modifier.LeadingTrivia)));
        }

        return declaration
            .WithModifiers(modifiers)
            .WithKeyword(declaration.Keyword.WithLeadingTrivia(modifier.LeadingTrivia));
    }
}
