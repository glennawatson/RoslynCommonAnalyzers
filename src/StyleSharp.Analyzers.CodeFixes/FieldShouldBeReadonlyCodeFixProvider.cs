// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Adds <c>readonly</c> to a field reported by SST1424.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FieldShouldBeReadonlyCodeFixProvider))]
[Shared]
public sealed class FieldShouldBeReadonlyCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.FieldShouldBeReadonly.Id);

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<FieldDeclarationSyntax>() is not { } field)
            {
                continue;
            }

            var updated = field.WithModifiers(field.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Make field readonly",
                    cancellationToken => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(field, updated))),
                    equivalenceKey: nameof(FieldShouldBeReadonlyCodeFixProvider)),
                diagnostic);
        }
    }
}
