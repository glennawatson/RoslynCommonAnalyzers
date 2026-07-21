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
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(DesignRules.FlagsEnumMissingZeroValue.Id);

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

        editor.ReplaceNode(declaration, (current, _) => AddNoneMember((EnumDeclarationSyntax)current));
    }

    /// <summary>Resolves the diagnostic's span to the enum it was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The enum declaration, or <see langword="null"/> when the shape no longer matches.</returns>
    private static EnumDeclarationSyntax? FindDeclaration(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<EnumDeclarationSyntax>();

    /// <summary>Inserts a <c>None = 0</c> member as the first member of the enum, matching its layout.</summary>
    /// <param name="declaration">The enum declaration.</param>
    /// <returns>The rewritten declaration.</returns>
    private static EnumDeclarationSyntax AddNoneMember(EnumDeclarationSyntax declaration)
    {
        var member = SyntaxFactory.EnumMemberDeclaration(NoneMemberName)
            .WithEqualsValue(SyntaxFactory.EqualsValueClause(
                SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0))));

        var members = declaration.Members;
        if (members.Count > 0)
        {
            return declaration.WithMembers(members.Insert(0, member.WithLeadingTrivia(members[0].GetLeadingTrivia())));
        }

        // An empty enum body: place the member on its own indented line and push the close brace down after it.
        var newLine = LineEndingHelper.GetLineBreak(declaration);
        var indent = SyntaxFactory.Whitespace(GetIndent(declaration) + "    ");
        var placed = member.WithLeadingTrivia(newLine, indent).WithTrailingTrivia(newLine);
        var closeBrace = declaration.CloseBraceToken.WithLeadingTrivia(SyntaxFactory.Whitespace(GetIndent(declaration)));
        return declaration
            .WithMembers(SyntaxFactory.SingletonSeparatedList(placed))
            .WithCloseBraceToken(closeBrace);
    }

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
