// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Adds a <c>private set;</c> to an injected or cascading component property that has no setter (SST2712), so
/// the runtime's reflection-based binding can assign it.
/// </summary>
/// <remarks>
/// The fix is offered only for an auto-property whose sole accessor is a bodyless <c>get;</c>: a
/// <c>private set;</c> is appended, giving the runtime a settable property while keeping the member encapsulated.
/// An expression-bodied property has no accessor list to extend and no backing field to assign, so no fix is
/// offered there — turning it into a settable property is a design change the fix does not make.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2712SetterlessInjectedPropertyCodeFixProvider))]
[Shared]
public sealed class Sst2712SetterlessInjectedPropertyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(FrameworksRules.SetterlessInjectedProperty.Id);

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
            if (Resolve(root, diagnostic) is not { } property)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add a private setter",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(property, AddPrivateSetter(property)))),
                    equivalenceKey: nameof(Sst2712SetterlessInjectedPropertyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, diagnostic) is not { } property)
        {
            return;
        }

        editor.ReplaceNode(property, (current, _) => AddPrivateSetter((PropertyDeclarationSyntax)current));
    }

    /// <summary>Resolves the reported auto-property, or <see langword="null"/> when no fix is offered.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>An auto-property with only a bodyless <c>get;</c>, or <see langword="null"/>.</returns>
    private static PropertyDeclarationSyntax? Resolve(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<PropertyDeclarationSyntax>() is not { AccessorList: { } accessorList, ExpressionBody: null } property)
        {
            return null;
        }

        var hasBodylessGet = false;
        var accessors = accessorList.Accessors;
        for (var i = 0; i < accessors.Count; i++)
        {
            var accessor = accessors[i];
            if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration) || accessor.IsKind(SyntaxKind.InitAccessorDeclaration))
            {
                return null;
            }

            if (accessor.IsKind(SyntaxKind.GetAccessorDeclaration) && accessor.Body is null && accessor.ExpressionBody is null)
            {
                hasBodylessGet = true;
            }
        }

        return hasBodylessGet ? property : null;
    }

    /// <summary>Appends a <c>private set;</c> accessor to an auto-property.</summary>
    /// <param name="property">The get-only auto-property.</param>
    /// <returns>The property with a private setter.</returns>
    private static PropertyDeclarationSyntax AddPrivateSetter(PropertyDeclarationSyntax property)
    {
        var setter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        return property
            .WithAccessorList(property.AccessorList!.AddAccessors(setter))
            .WithAdditionalAnnotations(Formatter.Annotation);
    }
}
