// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Adds a <c>None = 0</c> member to the front of a flags enum that declares no zero value (SST2329), giving
/// the empty set the name every other combination already has.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2329FlagsEnumMissingZeroValueCodeFixProvider))]
[Shared]
public sealed class Sst2329FlagsEnumMissingZeroValueCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The conventional name for a flags enum's zero-valued member.</summary>
    private const string NoneMemberName = "None";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(DesignRules.FlagsEnumMissingZeroValue.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

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
            if (FindDeclaration(root, diagnostic) is not { } declaration)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add 'None = 0'",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, AddNoneMember(declaration)))),
                    equivalenceKey: nameof(Sst2329FlagsEnumMissingZeroValueCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (FindDeclaration(editor.OriginalRoot, diagnostic) is not { } declaration)
        {
            return;
        }

        editor.ReplaceNode(declaration, static (current, _) => AddNoneMember((EnumDeclarationSyntax)current));
    }

    /// <summary>Resolves the diagnostic's span to the enum it was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The enum declaration, or <see langword="null"/> when the shape no longer matches.</returns>
    /// <remarks>
    /// A directive in the body takes the enum out of reach. Members inside an inactive <c>#if</c> are
    /// disabled text rather than members, so the fix would read the enum as empty and rewrite the close
    /// brace — taking the region and everything in it with the trivia that brace carries.
    /// </remarks>
    private static EnumDeclarationSyntax? FindDeclaration(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<EnumDeclarationSyntax>() is { } declaration
            && !DirectiveBoundaries.SeparateMembers(declaration)
            ? declaration
            : null;

    /// <summary>Inserts a <c>None = 0</c> member as the first member of the enum, matching its layout.</summary>
    /// <param name="declaration">The enum declaration.</param>
    /// <returns>The rewritten declaration.</returns>
    private static EnumDeclarationSyntax AddNoneMember(EnumDeclarationSyntax declaration)
    {
        var member = SyntaxFactory.EnumMemberDeclaration(
            attributeLists: default,
            modifiers: default,
            SyntaxFactory.Identifier(NoneMemberName),
            SyntaxFactory.EqualsValueClause(
                SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0))));

        var members = declaration.Members;
        if (members.Count > 0)
        {
            return declaration.WithMembers(members.Insert(0, member.WithLeadingTrivia(IndentOf(members[0]))));
        }

        // An empty enum body: place the member on its own indented line and push the close brace down after it.
        var newLine = LineEndingHelper.GetLineBreak(declaration);
        var indent = SyntaxFactory.Whitespace($"{GetIndent(declaration)}    ");
        var placed = member.Update(
            member.AttributeLists,
            member.Modifiers,
            member.Identifier.WithLeadingTrivia(newLine, indent),
            member.EqualsValue!.WithTrailingTrivia(newLine));
        var closeBrace = declaration.CloseBraceToken.WithLeadingTrivia(SyntaxFactory.Whitespace(GetIndent(declaration)));
        return declaration.Update(
            declaration.AttributeLists,
            declaration.Modifiers,
            declaration.EnumKeyword,
            declaration.Identifier,
            declaration.BaseList,
            declaration.OpenBraceToken,
            SyntaxFactory.SingletonSeparatedList(placed),
            closeBrace,
            declaration.SemicolonToken);
    }

    /// <summary>Gets the whitespace that positions a member, without what the author wrote above it.</summary>
    /// <param name="member">The member whose line the new one joins.</param>
    /// <returns>The run of layout trivia immediately before the member.</returns>
    /// <remarks>
    /// Only the indentation is shared. Copying the whole leading trivia would put a second copy of the
    /// member's documentation on the generated one, and a second <c>#region</c> or <c>#if</c> in the file
    /// with only one close to match it.
    /// </remarks>
    private static SyntaxTriviaList IndentOf(EnumMemberDeclarationSyntax member)
    {
        var leading = member.GetLeadingTrivia();
        var start = leading.Count;
        while (start > 0 && IsLayout(leading[start - 1]))
        {
            start--;
        }

        var layout = new List<SyntaxTrivia>(leading.Count - start);
        for (var i = start; i < leading.Count; i++)
        {
            layout.Add(leading[i]);
        }

        return SyntaxFactory.TriviaList(layout);
    }

    /// <summary>Returns whether a trivia only positions the node.</summary>
    /// <param name="trivia">The trivia to classify.</param>
    /// <returns><see langword="true"/> for whitespace and line breaks.</returns>
    private static bool IsLayout(in SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia);

    /// <summary>Gets the enum declaration's own indentation from its leading trivia.</summary>
    /// <param name="declaration">The enum declaration.</param>
    /// <returns>The leading whitespace, or an empty string when the enum starts at column zero.</returns>
    private static string GetIndent(EnumDeclarationSyntax declaration)
    {
        var leading = declaration.GetLeadingTrivia();
        return leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? leading[leading.Count - 1].ToString()
            : string.Empty;
    }
}
