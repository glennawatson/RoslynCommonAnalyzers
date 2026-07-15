// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Adds the <c>sealed</c> modifier to an attribute class (PSH1401).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1401SealAttributeTypesCodeFixProvider))]
[Shared]
public sealed class Psh1401SealAttributeTypesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.SealAttributeTypes.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Seal the attribute type", nameof(Psh1401SealAttributeTypesCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Adds the <c>sealed</c> modifier to the reported class declaration.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="declaration">The class declaration to seal.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ClassDeclarationSyntax declaration)
        => document.WithSyntaxRoot(root.ReplaceNode(declaration, SealedModifierRewrite.AddSealed(declaration)));

    /// <summary>Resolves the reported class declaration and builds its sealed replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ClassDeclarationSyntax>() is { } declaration
            ? new NodeReplacement(declaration, SealedModifierRewrite.AddSealed(declaration))
            : null;
}
