// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DesignRules.CollectionPropertyShouldBeReadOnly.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Remove the setter",
            nameof(Sst2305CollectionPropertyShouldBeReadOnlyCodeFixProvider),
            CanRewrite,
            TryRewrite);

    /// <summary>Resolves the reported property and builds the get-only replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) => root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not PropertyDeclarationSyntax property
            || RemoveSetter(property) is not { } updated
        ? null
        : new NodeReplacement(property, updated);

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is PropertyDeclarationSyntax property
            && Sst2305CollectionPropertyShouldBeReadOnlyAnalyzer.FindRemovableSetter(property)is not null
            && property.AccessorList is not null;

    /// <summary>Builds the property without its setter.</summary>
    /// <param name="property">The reported property.</param>
    /// <returns>The get-only property, or <see langword="null"/> when there is no setter to remove.</returns>
    private static PropertyDeclarationSyntax? RemoveSetter(PropertyDeclarationSyntax property) => Sst2305CollectionPropertyShouldBeReadOnlyAnalyzer.FindRemovableSetter(property) is not { } setter
            || property.AccessorList is not { } accessorList
        ? null
        : property.WithAccessorList(accessorList.WithAccessors(accessorList.Accessors.Remove(setter)));
}
