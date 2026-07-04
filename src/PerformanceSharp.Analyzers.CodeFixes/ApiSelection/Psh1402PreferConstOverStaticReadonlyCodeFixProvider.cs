// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Replaces the <c>static readonly</c> modifiers with <c>const</c> (PSH1402).</summary>
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

    /// <summary>Resolves the reported field declaration and builds its <c>const</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<FieldDeclarationSyntax>() is { } field
            ? new NodeReplacement(field, Rewrite(field))
            : null;

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
