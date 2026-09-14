// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Replaces a record property's <c>set</c> accessor with an <c>init</c> accessor (SST1802),
/// preserving its attributes, modifiers, body or expression body, and trivia.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RecordInitOnlyCodeFixProvider))]
[Shared]
public sealed class RecordInitOnlyCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(FindSetAccessor, static (current, _) => ToInitAccessor((AccessorDeclarationSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(RecordRules.InitOnlyProperty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Use 'init' accessor",
            nameof(RecordInitOnlyCodeFixProvider),
            FindSetAccessor,
            ConvertAsync);

    /// <summary>Replaces the set accessor with an equivalent init accessor.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="accessor">The set accessor to convert.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> ConvertAsync(Document document, AccessorDeclarationSyntax accessor, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        var initAccessor = ToInitAccessor(accessor);

        return document.WithSyntaxRoot(root!.ReplaceNode(accessor, initAccessor));
    }

    /// <summary>Resolves a diagnostic to the set accessor it was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The accessor, or <see langword="null"/> when the shape no longer matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AccessorDeclarationSyntax? FindSetAccessor(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindToken(diagnostic.Location.SourceSpan.Start).Parent as AccessorDeclarationSyntax;

    /// <summary>Builds the init accessor that replaces a set accessor, keeping everything else it had.</summary>
    /// <param name="accessor">The set accessor to convert.</param>
    /// <returns>The equivalent init accessor.</returns>
    /// <remarks>
    /// Every part is passed to the factory at once. Setting them one at a time through the With
    /// mutators builds and discards a whole accessor per call, six of them for this shape.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AccessorDeclarationSyntax ToInitAccessor(AccessorDeclarationSyntax accessor) =>
        SyntaxFactory.AccessorDeclaration(
            SyntaxKind.InitAccessorDeclaration,
            accessor.AttributeLists,
            accessor.Modifiers,
            SyntaxFactory.Token(accessor.Keyword.LeadingTrivia, SyntaxKind.InitKeyword, accessor.Keyword.TrailingTrivia),
            accessor.Body,
            accessor.ExpressionBody,
            accessor.SemicolonToken);
}
