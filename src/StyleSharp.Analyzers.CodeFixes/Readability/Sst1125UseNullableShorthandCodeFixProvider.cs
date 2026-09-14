// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Rewrites a long-form <c>Nullable&lt;T&gt;</c> type as the <c>T?</c> shorthand (SST1125).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1125UseNullableShorthandCodeFixProvider))]
[Shared]
public sealed class Sst1125UseNullableShorthandCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseNullableShorthand.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use 'T?' shorthand",
            nameof(Sst1125UseNullableShorthandCodeFixProvider),
            static (root, diagnostic) => FindGeneric(root.FindNode(diagnostic.Location.SourceSpan)) is not null,
            TryRewrite);

    /// <summary>Resolves the reported nullable type and builds its <c>T?</c> shorthand.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var outer = root.FindNode(diagnostic.Location.SourceSpan);
        return FindGeneric(outer) is { } generic
            ? new NodeReplacement(outer, CreateShorthand(outer, generic))
            : null;
    }

    /// <summary>Builds the <c>T?</c> shorthand that takes the reported type's place and trivia.</summary>
    /// <param name="outer">The full type node being replaced.</param>
    /// <param name="generic">The <c>Nullable&lt;T&gt;</c> generic name.</param>
    /// <returns>The shorthand type.</returns>
    private static NullableTypeSyntax CreateShorthand(SyntaxNode outer, GenericNameSyntax generic)
    {
        var elementType = generic.TypeArgumentList.Arguments[0].WithoutTrivia();
        return SyntaxFactory.NullableType(
            elementType.WithLeadingTrivia(outer.GetLeadingTrivia()),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.QuestionToken, outer.GetTrailingTrivia()));
    }

    /// <summary>Finds the <c>Nullable&lt;T&gt;</c> generic name inside the reported type node.</summary>
    /// <param name="outer">The reported type node.</param>
    /// <returns>The generic name, or <see langword="null"/> when none is present.</returns>
    private static GenericNameSyntax? FindGeneric(SyntaxNode outer) => outer switch
    {
        GenericNameSyntax generic => generic,
        QualifiedNameSyntax { Right: GenericNameSyntax generic } => generic,
        _ => null
    };
}
