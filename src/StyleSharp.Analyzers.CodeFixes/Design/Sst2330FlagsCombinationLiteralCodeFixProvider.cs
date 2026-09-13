// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a flags-enum member's numeric literal into the OR of the members it combines (SST2330) — <c>7</c>
/// becomes <c>Read | Write | Execute</c> — so the value states its own meaning and survives a renumbering of
/// the members it names.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2330FlagsCombinationLiteralCodeFixProvider))]
[Shared]
public sealed class Sst2330FlagsCombinationLiteralCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(DesignRules.FlagsCombinationLiteralShouldNameMembers.Id);

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
            if (Resolve(root, diagnostic) is not var (literal, replacement))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Name the combined flags",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(literal, replacement))),
                    equivalenceKey: nameof(Sst2330FlagsCombinationLiteralCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, diagnostic) is not var (literal, replacement))
        {
            return;
        }

        editor.ReplaceNode(literal, replacement);
    }

    /// <summary>Resolves the diagnostic to the literal to replace and its OR-of-members replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The literal and its replacement, or <see langword="null"/> when the shape no longer matches.</returns>
    private static (LiteralExpressionSyntax Literal, ExpressionSyntax Replacement)? Resolve(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<LiteralExpressionSyntax>() is not { } literal
            || !diagnostic.Properties.TryGetValue(Sst2330FlagsCombinationLiteralAnalyzer.MembersKey, out var members)
            || string.IsNullOrEmpty(members))
        {
            return null;
        }

        ExpressionSyntax? replacement = null;
        foreach (var token in SyntaxFactory.ParseTokens(members!))
        {
            if (token.IsKind(SyntaxKind.CommaToken) || token.IsKind(SyntaxKind.EndOfFileToken))
            {
                continue;
            }

            if (!token.IsKind(SyntaxKind.IdentifierToken) || token.ContainsDiagnostics)
            {
                return (literal, SyntaxFactory.ParseExpression(members!.Replace(",", " | ")).WithTriviaFrom(literal));
            }

            replacement = AppendMember(replacement, literal, token, members!.Length);
        }

        return replacement is null ? null : (literal, replacement);
    }

    /// <summary>Appends a named flag while retaining the literal's outer trivia.</summary>
    /// <param name="replacement">The flags already combined, or null for the first member.</param>
    /// <param name="literal">The literal being replaced.</param>
    /// <param name="token">The member's identifier token.</param>
    /// <param name="membersLength">The length of the diagnostic's member list.</param>
    /// <returns>The member name or the extended OR expression.</returns>
    private static ExpressionSyntax AppendMember(ExpressionSyntax? replacement, LiteralExpressionSyntax literal, in SyntaxToken token, int membersLength)
    {
        var space = SyntaxFactory.TriviaList(SyntaxFactory.Space);
        var name = SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
            replacement is null ? literal.GetLeadingTrivia() : default,
            SyntaxKind.None,
            token.Text,
            token.ValueText,
            token.Span.End == membersLength ? literal.GetTrailingTrivia() : space));
        return replacement is null
            ? name
            : SyntaxFactory.BinaryExpression(
                SyntaxKind.BitwiseOrExpression,
                replacement,
                SyntaxFactory.Token(default, SyntaxKind.BarToken, space),
                name);
    }
}
