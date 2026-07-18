// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes the redundant explicit type arguments from a method call (SST2251).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2251InferableTypeArgumentsCodeFixProvider))]
[Shared]
public sealed class Sst2251InferableTypeArgumentsCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.OmitInferableTypeArguments.Id);

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
            if (FindGenericName(root, diagnostic.Location.SourceSpan) is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the redundant type arguments",
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: nameof(Sst2251InferableTypeArgumentsCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (FindGenericName(editor.OriginalRoot, diagnostic.Location.SourceSpan) is not { } genericName)
        {
            return;
        }

        editor.ReplaceNode(genericName, CreateReplacement(genericName));
    }

    /// <summary>Applies one SST2251 fix.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        if (FindGenericName(root, diagnostic.Location.SourceSpan) is not { } genericName)
        {
            return document;
        }

        return document.WithSyntaxRoot(root.ReplaceNode(genericName, CreateReplacement(genericName)));
    }

    /// <summary>Builds the plain identifier that replaces the generic name.</summary>
    /// <param name="genericName">The generic name being simplified.</param>
    /// <returns>The identifier name without the type-argument list.</returns>
    private static IdentifierNameSyntax CreateReplacement(GenericNameSyntax genericName)
        => SyntaxFactory.IdentifierName(genericName.Identifier).WithTriviaFrom(genericName);

    /// <summary>Finds the generic name whose type-argument list the diagnostic marks.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <returns>The generic name, or <see langword="null"/>.</returns>
    private static GenericNameSyntax? FindGenericName(SyntaxNode root, TextSpan span)
    {
        var node = root.FindToken(span.Start).Parent;
        while (node is not null)
        {
            if (node is GenericNameSyntax genericName)
            {
                return genericName;
            }

            node = node.Parent;
        }

        return null;
    }
}
