// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace StyleSharp.Analyzers;

/// <summary>
/// Makes a type that owns disposables <c>IDisposable</c> (SST2315) by adding the interface and a
/// <c>Dispose()</c> that releases the owned members. Offered only when every owned member is
/// synchronously disposable; a collection or an async-only member needs a design decision the fix does
/// not make.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2315OwnsDisposableFieldCodeFixProvider))]
[Shared]
public sealed class Sst2315OwnsDisposableFieldCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DesignRules.OwnsDisposableField.Id);

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
            if (Resolve(root, diagnostic) is not var (declaration, members))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Implement IDisposable and dispose the owned members",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, MakeDisposable(declaration, members)))),
                    equivalenceKey: nameof(Sst2315OwnsDisposableFieldCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, diagnostic) is not var (declaration, members))
        {
            return;
        }

        editor.ReplaceNode(declaration, (current, _) => MakeDisposable((TypeDeclarationSyntax)current, members));
    }

    /// <summary>Resolves the reported type declaration and the members its <c>Dispose()</c> should release.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The type declaration and member names, or <see langword="null"/> when no fix is offered.</returns>
    private static (TypeDeclarationSyntax Declaration, string[] Members)? Resolve(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaration
            || !diagnostic.Properties.TryGetValue(Sst2315OwnsDisposableFieldAnalyzer.MembersToDisposeKey, out var members)
            || string.IsNullOrEmpty(members))
        {
            return null;
        }

        return (declaration, members!.Split(','));
    }

    /// <summary>Adds <c>IDisposable</c> and a <c>Dispose()</c> that releases each owned member.</summary>
    /// <param name="declaration">The type declaration.</param>
    /// <param name="members">The members to release.</param>
    /// <returns>The updated declaration.</returns>
    private static TypeDeclarationSyntax MakeDisposable(TypeDeclarationSyntax declaration, string[] members)
    {
        var statements = new List<StatementSyntax>(members.Length);
        for (var i = 0; i < members.Length; i++)
        {
            statements.Add(SyntaxFactory.ParseStatement(members[i] + ".Dispose();"));
        }

        var dispose = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                "Dispose")
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithBody(SyntaxFactory.Block(statements));

        var baseType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("global::System.IDisposable"))
            .WithAdditionalAnnotations(Simplifier.Annotation);

        return BaseListInsertion.AddBaseType(declaration, baseType)
            .AddMembers(dispose)
            .WithAdditionalAnnotations(Formatter.Annotation);
    }
}
