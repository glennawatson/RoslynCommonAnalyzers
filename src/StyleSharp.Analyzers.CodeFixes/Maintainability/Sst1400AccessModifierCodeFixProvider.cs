// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers;

/// <summary>Adds the implicit access modifier to a declaration that omits one (SST1400).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1400AccessModifierCodeFixProvider))]
[Shared]
public sealed class Sst1400AccessModifierCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RegisterBatchEdits);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        MaintainabilityRules.AccessModifierDeclared.Id,
        OrderingRules.PartialElementAccess.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

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
            if (!TryGetTarget(root, diagnostic, out var member, out var modifier, out var accessibility))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Add '{modifier}' modifier",
                    _ => AddAsync(context.Document, root, member, accessibility),
                    equivalenceKey: nameof(Sst1400AccessModifierCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Registers the edits that fix one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal static void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetTarget(editor.OriginalRoot, diagnostic, out var member, out _, out var accessibility))
        {
            return;
        }

        editor.ReplaceNode(member, (current, generator) => generator.WithAccessibility(current, accessibility));
    }

    /// <summary>Applies the implicit accessibility to the member.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="member">The member that omits an access modifier.</param>
    /// <param name="accessibility">The accessibility to declare.</param>
    /// <returns>The updated document.</returns>
    /// <remarks>The rewrite is a pure syntax edit over a root the caller already has, so there is nothing to cancel.</remarks>
    internal static Task<Document> AddAsync(Document document, SyntaxNode root, MemberDeclarationSyntax member, Accessibility accessibility)
    {
        var generator = SyntaxGenerator.GetGenerator(document);
        var updated = generator.WithAccessibility(member, accessibility);
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(member, updated)));
    }

    /// <summary>Resolves a diagnostic to the member that omits its access modifier and the modifier the analyzer chose.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="member">The member that omits an access modifier.</param>
    /// <param name="modifier">The modifier the analyzer chose.</param>
    /// <param name="accessibility">The accessibility that modifier declares.</param>
    /// <returns><see langword="true"/> when the diagnostic names a modifier and still resolves to a member.</returns>
    private static bool TryGetTarget(
        SyntaxNode root,
        Diagnostic diagnostic,
        [NotNullWhen(true)] out MemberDeclarationSyntax? member,
        out string? modifier,
        out Accessibility accessibility)
    {
        member = null;
        accessibility = Accessibility.NotApplicable;
        if (!diagnostic.Properties.TryGetValue(Sst1400AccessModifierAnalyzer.ModifierKey, out modifier) || string.IsNullOrEmpty(modifier))
        {
            return false;
        }

        member = DiagnosticEnclosingNode.Find<MemberDeclarationSyntax>(root, diagnostic);
        if (member is null)
        {
            return false;
        }

        accessibility = string.Equals(modifier, "internal", StringComparison.Ordinal) ? Accessibility.Internal : Accessibility.Private;
        return true;
    }
}
