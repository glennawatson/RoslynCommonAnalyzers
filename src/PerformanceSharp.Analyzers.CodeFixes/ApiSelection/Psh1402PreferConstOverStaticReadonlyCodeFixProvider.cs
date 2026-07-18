// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Makes the reported declaration <c>const</c> (PSH1402): a field swaps its <c>static readonly</c>
/// modifiers for <c>const</c>; a local gains <c>const</c>, spelling out the type when it was <c>var</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1402PreferConstOverStaticReadonlyCodeFixProvider))]
[Shared]
public sealed class Psh1402PreferConstOverStaticReadonlyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.PreferConstOverStaticReadonly.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use const", nameof(Psh1402PreferConstOverStaticReadonlyCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported field or local declaration and builds its <c>const</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>() is { } local)
        {
            diagnostic.Properties.TryGetValue(Psh1402PreferConstOverStaticReadonlyAnalyzer.ExplicitTypeKey, out var explicitTypeName);
            return new NodeReplacement(local, Rewrite(local, explicitTypeName));
        }

        return node.FirstAncestorOrSelf<FieldDeclarationSyntax>() is { } field
            ? new NodeReplacement(field, Rewrite(field))
            : null;
    }

    /// <summary>Rewrites a local declaration to <c>const</c>, spelling out the type when it was <c>var</c>.</summary>
    /// <param name="local">The local declaration to rewrite.</param>
    /// <param name="explicitTypeName">The explicit type replacing <c>var</c>, or <see langword="null"/> when the type is already explicit.</param>
    /// <returns>The rewritten local declaration.</returns>
    private static LocalDeclarationStatementSyntax Rewrite(LocalDeclarationStatementSyntax local, string? explicitTypeName)
    {
        if (explicitTypeName is not null)
        {
            var type = local.Declaration.Type;
            local = local.WithDeclaration(local.Declaration.WithType(SyntaxFactory.ParseTypeName(explicitTypeName).WithTriviaFrom(type)));
        }

        var firstToken = local.GetFirstToken();
        var constKeyword = SyntaxFactory.Token(SyntaxKind.ConstKeyword)
            .WithLeadingTrivia(firstToken.LeadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.Space);
        local = local.ReplaceToken(firstToken, firstToken.WithLeadingTrivia());
        return local.WithModifiers(local.Modifiers.Insert(0, constKeyword));
    }

    /// <summary>Rewrites the modifier list, turning the first of <c>static</c>/<c>readonly</c> into <c>const</c> and dropping the other.</summary>
    /// <param name="field">The field declaration to rewrite.</param>
    /// <returns>The rewritten field declaration.</returns>
    private static FieldDeclarationSyntax Rewrite(FieldDeclarationSyntax field)
    {
        var modifiers = field.Modifiers;
        var rewritten = new SyntaxToken[modifiers.Count - 1];
        var write = 0;
        var constInserted = false;
        for (var i = 0; i < modifiers.Count; i++)
        {
            var token = modifiers[i];
            if (token.IsKind(SyntaxKind.StaticKeyword) || token.IsKind(SyntaxKind.ReadOnlyKeyword))
            {
                if (!constInserted)
                {
                    rewritten[write++] = SyntaxFactory.Token(SyntaxKind.ConstKeyword).WithTriviaFrom(token);
                    constInserted = true;
                }

                continue;
            }

            rewritten[write++] = token;
        }

        return field.WithModifiers(SyntaxFactory.TokenList(rewritten));
    }
}
