// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace StyleSharp.Analyzers;

/// <summary>
/// Adds the disposal interface a type's <c>Dispose</c>/<c>DisposeAsync</c> method already matches
/// (SST2316), so the owners that dispose through the interface reach the method.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2316DisposeWithoutInterfaceCodeFixProvider))]
[Shared]
public sealed class Sst2316DisposeWithoutInterfaceCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DesignRules.DisposeWithoutInterface.Id);

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
            if (Resolve(root, diagnostic) is not var (declaration, interfaceName))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Implement {interfaceName}",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, AddInterface(declaration, interfaceName)))),
                    equivalenceKey: nameof(Sst2316DisposeWithoutInterfaceCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, diagnostic) is not var (declaration, interfaceName))
        {
            return;
        }

        editor.ReplaceNode(declaration, (current, _) => AddInterface((TypeDeclarationSyntax)current, interfaceName));
    }

    /// <summary>Resolves the reported method to its type declaration and the interface to add.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The type declaration and interface name, or <see langword="null"/>.</returns>
    private static (TypeDeclarationSyntax Declaration, string InterfaceName)? Resolve(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaration
            || !diagnostic.Properties.TryGetValue(Sst2316DisposeWithoutInterfaceAnalyzer.InterfaceKey, out var interfaceName)
            || interfaceName is null)
        {
            return null;
        }

        return (declaration, interfaceName);
    }

    /// <summary>Adds the fully-qualified disposal interface to a type's base list.</summary>
    /// <param name="declaration">The type declaration.</param>
    /// <param name="interfaceName">The interface simple name.</param>
    /// <returns>The updated declaration.</returns>
    private static TypeDeclarationSyntax AddInterface(TypeDeclarationSyntax declaration, string interfaceName)
    {
        var baseType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("global::System." + interfaceName))
            .WithAdditionalAnnotations(Simplifier.Annotation);
        return BaseListInsertion.AddBaseType(declaration, baseType).WithAdditionalAnnotations(Formatter.Annotation);
    }
}
