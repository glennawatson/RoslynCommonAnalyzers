// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Adds <c>readonly</c> to a field reported by SST1424.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1424FieldShouldBeReadonlyCodeFixProvider))]
[Shared]
public sealed class Sst1424FieldShouldBeReadonlyCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(FindField, static (current, _) => WithReadonly((FieldDeclarationSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.FieldShouldBeReadonly.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Make field readonly",
            nameof(Sst1424FieldShouldBeReadonlyCodeFixProvider),
            FindField,
            AddReadonly);

    /// <summary>Adds the readonly modifier to the field declaration.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The current syntax root.</param>
    /// <param name="field">The field declaration to update.</param>
    /// <returns>The updated document.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Document AddReadonly(Document document, SyntaxNode root, FieldDeclarationSyntax field) =>
        document.WithSyntaxRoot(root.ReplaceNode(field, WithReadonly(field)));

    /// <summary>Resolves the field declaration a diagnostic was reported inside.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The field declaration, or <see langword="null"/> when the diagnostic is not inside one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FieldDeclarationSyntax? FindField(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<FieldDeclarationSyntax>();

    /// <summary>Appends the readonly modifier to a field declaration.</summary>
    /// <param name="field">The field declaration.</param>
    /// <returns>The field declaration carrying <c>readonly</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FieldDeclarationSyntax WithReadonly(FieldDeclarationSyntax field) =>
        field.WithModifiers(field.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));
}
