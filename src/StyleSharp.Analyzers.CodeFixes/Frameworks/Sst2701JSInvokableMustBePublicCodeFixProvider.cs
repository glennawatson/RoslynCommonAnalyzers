// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Makes a <c>[JSInvokable]</c> method public so JavaScript interop can reach it (SST2701).
/// </summary>
/// <remarks>
/// The fix replaces whatever accessibility the method declared with <c>public</c>, which is the accessibility the
/// attribute already implied. A method that explicitly implements an interface carries no accessibility modifier to
/// change, so no fix is offered there — the attribute belongs on an ordinary public method, not an explicit
/// implementation.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2701JSInvokableMustBePublicCodeFixProvider))]
[Shared]
public sealed class Sst2701JSInvokableMustBePublicCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(FrameworksRules.JSInvokableMustBePublic.Id);

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
            if (FindDeclaration(root, diagnostic) is not { } method)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Make the method public",
                    _ => Task.FromResult(Apply(context.Document, root, method)),
                    equivalenceKey: nameof(Sst2701JSInvokableMustBePublicCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (FindDeclaration(editor.OriginalRoot, diagnostic) is not { } method)
        {
            return;
        }

        editor.ReplaceNode(method, (current, generator) => generator.WithAccessibility(current, Accessibility.Public));
    }

    /// <summary>Applies the fix for one non-public invokable method.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="method">The method declaration to make public.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, MethodDeclarationSyntax method)
    {
        var generator = SyntaxGenerator.GetGenerator(document);
        return document.WithSyntaxRoot(root.ReplaceNode(method, generator.WithAccessibility(method, Accessibility.Public)));
    }

    /// <summary>Resolves the diagnostic's span to the ordinary method it was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The method declaration, or <see langword="null"/> when the shape no longer matches or is an explicit implementation.</returns>
    private static MethodDeclarationSyntax? FindDeclaration(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>() is { ExplicitInterfaceSpecifier: null } method
            ? method
            : null;
}
