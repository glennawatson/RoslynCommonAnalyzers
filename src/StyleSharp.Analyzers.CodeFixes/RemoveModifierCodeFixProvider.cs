// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes the modifier reported by SST1419.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveModifierCodeFixProvider))]
[Shared]
public sealed class RemoveModifierCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.NoRedundantModifier.Id);

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

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            if (token.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } declaration)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove redundant modifier",
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(
                            root.ReplaceNode(declaration, declaration.WithModifiers(declaration.Modifiers.Remove(token))))),
                    equivalenceKey: nameof(RemoveModifierCodeFixProvider)),
                diagnostic);
        }
    }
}
