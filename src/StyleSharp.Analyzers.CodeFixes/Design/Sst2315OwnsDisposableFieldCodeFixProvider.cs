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
    private static (TypeDeclarationSyntax Declaration, string Members)? Resolve(SyntaxNode root, Diagnostic diagnostic)
    {
        // The member is appended after the last one and before the closing brace, which is where the
        // directive closing a region over the tail sits — so the new member would land inside it.
        return root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaration
            || DirectiveBoundaries.SeparateMembers(declaration)
            || !diagnostic.Properties.TryGetValue(Sst2315OwnsDisposableFieldAnalyzer.MembersToDisposeKey, out var members)
            || string.IsNullOrEmpty(members)
            ? null
            : (declaration, members!);
    }

    /// <summary>Adds <c>IDisposable</c> and a <c>Dispose()</c> that releases each owned member.</summary>
    /// <param name="declaration">The type declaration.</param>
    /// <param name="members">The members to release.</param>
    /// <returns>The updated declaration.</returns>
    private static TypeDeclarationSyntax MakeDisposable(TypeDeclarationSyntax declaration, string members)
    {
        const string DisposeCall = ".Dispose();";
        var memberCount = 1;
        foreach (var character in members)
        {
            if (character == ',')
            {
                memberCount++;
            }
        }

        var statements = new List<StatementSyntax>(memberCount);
        if (memberCount == 1)
        {
            statements.Add(SyntaxFactory.ParseStatement(members + DisposeCall));
        }
        else
        {
            var buffer = new char[members.Length + DisposeCall.Length];
            var start = 0;
            for (var i = 0; i < memberCount; i++)
            {
                var end = members.IndexOf(',', start);
                var length = (end < 0 ? members.Length : end) - start;
                members.CopyTo(start, buffer, 0, length);
                DisposeCall.CopyTo(0, buffer, length, DisposeCall.Length);
                statements.Add(SyntaxFactory.ParseStatement(new(buffer, 0, length + DisposeCall.Length)));
                start += length + 1;
            }
        }

        var dispose = SyntaxFactory.MethodDeclaration(
            attributeLists: default,
            SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)),
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
            explicitInterfaceSpecifier: null,
            SyntaxFactory.Identifier("Dispose"),
            typeParameterList: null,
            SyntaxFactory.ParameterList(),
            constraintClauses: default,
            SyntaxFactory.Block(statements),
            expressionBody: null,
            semicolonToken: default);

        var baseType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("global::System.IDisposable"))
            .WithAdditionalAnnotations(Simplifier.Annotation);

        return BaseListInsertion.AddBaseType(declaration, baseType)
            .AddMembers(dispose)
            .WithAdditionalAnnotations(Formatter.Annotation);
    }
}
