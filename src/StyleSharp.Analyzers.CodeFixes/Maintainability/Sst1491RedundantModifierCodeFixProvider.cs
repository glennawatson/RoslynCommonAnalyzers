// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a modifier that restates the declaration's default (SST1491).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1491RedundantModifierCodeFixProvider))]
[Shared]
public sealed class Sst1491RedundantModifierCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.RedundantModifier.Id);

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
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            if (token.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } declaration
                || declaration.Modifiers.IndexOf(token) < 0)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Remove the redundant '{token.ValueText}' modifier",
                    _ => Task.FromResult(RemoveModifierCodeFixProvider.RemoveModifier(context.Document, root, declaration, token)),
                    equivalenceKey: nameof(Sst1491RedundantModifierCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        var token = editor.OriginalRoot.FindToken(diagnostic.Location.SourceSpan.Start);
        if (token.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } declaration
            || declaration.Modifiers.IndexOf(token) < 0)
        {
            return;
        }

        // One interface member can carry two redundant modifiers, and the second edit runs against a list the
        // first has already shortened — so the modifier is found by kind in the current node rather than by
        // its index in the original. A modifier kind cannot repeat within one list, so the match is exact.
        var kind = token.Kind();
        editor.ReplaceNode(declaration, (current, _) => RemoveModifierOfKind((MemberDeclarationSyntax)current, kind));
    }

    /// <summary>Removes the modifier of one kind from a declaration, if it is still there.</summary>
    /// <param name="member">The current member declaration, including any edits already applied.</param>
    /// <param name="kind">The modifier kind to remove.</param>
    /// <returns>The member without the modifier.</returns>
    private static MemberDeclarationSyntax RemoveModifierOfKind(MemberDeclarationSyntax member, SyntaxKind kind)
    {
        var modifiers = member.Modifiers;
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (!modifiers[i].IsKind(kind))
            {
                continue;
            }

            // Removing the first modifier would otherwise take the declaration's indentation with it.
            return member
                .WithModifiers(modifiers.RemoveAt(i))
                .WithLeadingTrivia(member.GetLeadingTrivia());
        }

        return member;
    }
}
