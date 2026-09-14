// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Seals a class that implements <c>IEquatable&lt;T&gt;</c> against itself (SST2301).</summary>
/// <remarks>
/// The fix says what the type already meant: equality is decided here and nowhere else. If the class is
/// already derived from — in this project or another — sealing it will not compile, and that failure is
/// the honest one: the hierarchy and the contract were never compatible, and the answer is to move
/// equality somewhere a hierarchy can keep it, not to leave the type open and the equality asymmetric.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2301SealEquatableTypeCodeFixProvider))]
[Shared]
public sealed class Sst2301SealEquatableTypeCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(FindDeclaration, static (current, _) => MakeSealed((ClassDeclarationSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DesignRules.EquatableTypeShouldBeSealed.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(context, "Seal the type", nameof(Sst2301SealEquatableTypeCodeFixProvider), FindDeclaration, MakeSealed);

    /// <summary>Builds the class declaration with <c>sealed</c> inserted after the access modifiers.</summary>
    /// <param name="declaration">The class declaration to seal.</param>
    /// <returns>The rewritten declaration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ClassDeclarationSyntax MakeSealed(ClassDeclarationSyntax declaration) =>
        ClassModifierInsertion.InsertBeforePartial(declaration, SyntaxKind.SealedKeyword, takePartialIndentation: true);

    /// <summary>Resolves the diagnostic's span to the class it was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The class declaration, or <see langword="null"/> when the shape no longer matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ClassDeclarationSyntax? FindDeclaration(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ClassDeclarationSyntax>();
}
