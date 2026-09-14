// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Adds <c>static</c> to a partial class part that omits it while another part declares it (SST2335), so each
/// part states the type's static-ness on its own.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2335PartialStaticMismatchCodeFixProvider))]
[Shared]
public sealed class Sst2335PartialStaticMismatchCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(FindDeclaration, static (current, _) => MakeStatic((ClassDeclarationSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(DesignRules.PartialTypeStaticModifierMismatch.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Add 'static'",
            nameof(Sst2335PartialStaticMismatchCodeFixProvider),
            FindDeclaration,
            MakeStatic);

    /// <summary>Resolves the diagnostic's span to the class part it was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The class declaration, or <see langword="null"/> when the shape no longer matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ClassDeclarationSyntax? FindDeclaration(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<ClassDeclarationSyntax>();

    /// <summary>Builds the class declaration with <c>static</c> inserted before <c>partial</c>.</summary>
    /// <param name="declaration">The class part to make static.</param>
    /// <returns>The rewritten declaration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ClassDeclarationSyntax MakeStatic(ClassDeclarationSyntax declaration) =>
        ClassModifierInsertion.InsertBeforePartial(declaration, SyntaxKind.StaticKeyword, takePartialIndentation: true);
}
