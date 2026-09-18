// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.FindSymbols;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes the <c>set</c> accessor from a settable collection property (SST2305). The accessor is
/// deleted from the list it sits in, so the property keeps its own layout — a one-line
/// <c>{ get; set; }</c> becomes <c>{ get; }</c>, and a block-bodied accessor list loses one accessor
/// and nothing else.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2305CollectionPropertyShouldBeReadOnlyCodeFixProvider))]
[Shared]
public sealed class Sst2305CollectionPropertyShouldBeReadOnlyCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly AsyncBatchEditFixAllProvider FixAll = new(RegisterEditsAsync);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DesignRules.CollectionPropertyShouldBeReadOnly.Id);

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
            if (!await CanRewriteAsync(context.Document, root, diagnostic, context.CancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the setter",
                    _ => Task.FromResult(ReplaceNodeCodeFix.Apply(context.Document, root, diagnostic, TryRewrite)),
                    nameof(Sst2305CollectionPropertyShouldBeReadOnlyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Removes a setter only when no reference in the solution writes to the property.</summary>
    /// <param name="editor">The document editor.</param>
    /// <param name="diagnostic">The property diagnostic.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous edit registration.</returns>
    internal static async Task RegisterEditsAsync(DocumentEditor editor, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        if (await CanRewriteAsync(editor.OriginalDocument, editor.OriginalRoot, diagnostic, cancellationToken).ConfigureAwait(false))
        {
            ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);
        }
    }

    /// <summary>Resolves the reported property and builds the get-only replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) => root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not PropertyDeclarationSyntax property
            || RemoveSetter(property) is not { } updated
        ? null
        : new NodeReplacement(property, updated);

    /// <summary>Checks the setter's shape and its writers without constructing replacement syntax.</summary>
    /// <param name="document">The document containing the property.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the reported property can lose its setter.</returns>
    private static async Task<bool> CanRewriteAsync(Document document, SyntaxNode root, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not PropertyDeclarationSyntax property
            || Sst2305CollectionPropertyShouldBeReadOnlyAnalyzer.FindRemovableSetter(property) is null
            || await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false) is not { } model
            || model.GetDeclaredSymbol(property, cancellationToken) is not { } symbol)
        {
            return false;
        }

        var references = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution, cancellationToken).ConfigureAwait(false);
        foreach (var reference in references)
        {
            foreach (var location in reference.Locations)
            {
                var referenceRoot = await location.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                if (referenceRoot is null || IsWrite(referenceRoot.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Recognizes references that assign the property rather than mutate its contents.</summary>
    /// <param name="reference">The referencing syntax.</param>
    /// <returns>Whether the reference needs a setter.</returns>
    private static bool IsWrite(SyntaxNode reference)
    {
        var access = reference;
        while (true)
        {
            switch (access.Parent)
            {
                case MemberAccessExpressionSyntax member when member.Name == access:
                {
                    access = member;
                    break;
                }

                case MemberBindingExpressionSyntax binding:
                {
                    access = binding;
                    break;
                }

                case ParenthesizedExpressionSyntax parentheses:
                {
                    access = parentheses;
                    break;
                }

                case ArgumentSyntax { Parent: TupleExpressionSyntax tuple }:
                {
                    access = tuple;
                    break;
                }

                default:
                    return RequiresSetter(access);
            }
        }
    }

    /// <summary>Checks the operation surrounding an unwrapped property access.</summary>
    /// <param name="access">The property access or assignment tuple containing it.</param>
    /// <returns>Whether the surrounding operation invokes a setter.</returns>
    private static bool RequiresSetter(SyntaxNode access) => access.Parent switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left == access && assignment.Right is not InitializerExpressionSyntax,
        PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression),
        PostfixUnaryExpressionSyntax postfix => postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression),
        NameEqualsSyntax { Parent: AttributeArgumentSyntax } => true,
        _ => false,
    };

    /// <summary>Builds the property without its setter.</summary>
    /// <param name="property">The reported property.</param>
    /// <returns>The get-only property, or <see langword="null"/> when there is no setter to remove.</returns>
    private static PropertyDeclarationSyntax? RemoveSetter(PropertyDeclarationSyntax property) => Sst2305CollectionPropertyShouldBeReadOnlyAnalyzer.FindRemovableSetter(property) is not { } setter
            || property.AccessorList is not { } accessorList
        ? null
        : property.WithAccessorList(accessorList.WithAccessors(accessorList.Accessors.Remove(setter)));
}
