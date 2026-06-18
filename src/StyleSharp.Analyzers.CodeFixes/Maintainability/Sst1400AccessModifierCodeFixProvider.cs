// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers;

/// <summary>Adds the implicit access modifier to a declaration that omits one (SST1400).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1400AccessModifierCodeFixProvider))]
[Shared]
public sealed class Sst1400AccessModifierCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        MaintainabilityRules.AccessModifierDeclared.Id,
        OrderingRules.PartialElementAccess.Id);

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
            if (!diagnostic.Properties.TryGetValue(Sst1400AccessModifierAnalyzer.ModifierKey, out var modifier) || string.IsNullOrEmpty(modifier))
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
                    cancellationToken => AddAsync(context.Document, root, member, accessibility, cancellationToken),
                    equivalenceKey: nameof(Sst1400AccessModifierCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!diagnostic.Properties.TryGetValue(Sst1400AccessModifierAnalyzer.ModifierKey, out var modifier) || string.IsNullOrEmpty(modifier))
        {
            return;
        }

        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } member)
        {
            return;
        }

        var accessibility = string.Equals(modifier, "internal", StringComparison.Ordinal) ? Accessibility.Internal : Accessibility.Private;
        editor.ReplaceNode(member, (current, generator) => generator.WithAccessibility(current, accessibility));
    }

    /// <summary>Applies the implicit accessibility to the member.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="member">The member that omits an access modifier.</param>
    /// <param name="accessibility">The accessibility to declare.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static Task<Document> AddAsync(Document document, SyntaxNode root, MemberDeclarationSyntax member, Accessibility accessibility, CancellationToken cancellationToken)
    {
        var generator = SyntaxGenerator.GetGenerator(document);
        var updated = generator.WithAccessibility(member, accessibility);
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(member, updated)));
    }

    /// <summary>Applies the implicit accessibility to the member.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="member">The member that omits an access modifier.</param>
    /// <param name="accessibility">The accessibility to declare.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> AddAsync(Document document, MemberDeclarationSyntax member, Accessibility accessibility, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return await AddAsync(document, root!, member, accessibility, cancellationToken).ConfigureAwait(false);
    }
}
