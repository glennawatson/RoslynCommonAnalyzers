// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers;

/// <summary>Adds the implicit access modifier to a declaration that omits one (SST1400).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AccessModifierCodeFixProvider))]
[Shared]
public sealed class AccessModifierCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        MaintainabilityRules.AccessModifierDeclared.Id,
        OrderingRules.PartialElementAccess.Id);

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
            if (!diagnostic.Properties.TryGetValue(AccessModifierAnalyzer.ModifierKey, out var modifier) || string.IsNullOrEmpty(modifier))
            {
                continue;
            }

            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } member)
            {
                continue;
            }

            var accessibility = string.Equals(modifier, "internal", StringComparison.Ordinal) ? Accessibility.Internal : Accessibility.Private;
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Add '{modifier}' modifier",
                    cancellationToken => AddAsync(context.Document, member, accessibility, cancellationToken),
                    equivalenceKey: nameof(AccessModifierCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Applies the implicit accessibility to the member.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="member">The member that omits an access modifier.</param>
    /// <param name="accessibility">The accessibility to declare.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> AddAsync(Document document, MemberDeclarationSyntax member, Accessibility accessibility, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var generator = SyntaxGenerator.GetGenerator(document);
        var updated = generator.WithAccessibility(member, accessibility);
        return document.WithSyntaxRoot(root!.ReplaceNode(member, updated));
    }
}
