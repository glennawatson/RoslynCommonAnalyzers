// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Removes the redundant explicit type arguments from a method call (SST2251).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2251InferableTypeArgumentsCodeFixProvider))]
[Shared]
public sealed class Sst2251InferableTypeArgumentsCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(FindGenericName, static (current, _) => CreateReplacement((GenericNameSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.OmitInferableTypeArguments.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Remove the redundant type arguments",
            nameof(Sst2251InferableTypeArgumentsCodeFixProvider),
            FindGenericName,
            CreateReplacement);

    /// <summary>Builds the plain identifier that replaces the generic name.</summary>
    /// <param name="genericName">The generic name being simplified.</param>
    /// <returns>The identifier name without the type-argument list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IdentifierNameSyntax CreateReplacement(GenericNameSyntax genericName) =>
        SyntaxFactory.IdentifierName(genericName.Identifier.WithTrailingTrivia(genericName.GetTrailingTrivia()));

    /// <summary>Finds the generic name whose type-argument list the diagnostic marks.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The generic name, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GenericNameSyntax? FindGenericName(SyntaxNode root, Diagnostic diagnostic) =>
        DiagnosticAncestor.Find<GenericNameSyntax>(root, diagnostic.Location.SourceSpan);
}
