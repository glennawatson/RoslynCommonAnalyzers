// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Folds a nested property pattern into the extended property path it is equivalent to (SST2238).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2238NestedPropertyPatternCodeFixProvider))]
[Shared]
public sealed class Sst2238NestedPropertyPatternCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(ModernSyntaxRules.SimplifyNestedPropertyPattern.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Flatten the property pattern",
            nameof(Sst2238NestedPropertyPatternCodeFixProvider),
            CanRewrite,
            TryRewrite);

    /// <summary>Checks the property-only shape before deferring path parsing to the action.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether both property names are valid in a flattenable subpattern.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<SubpatternSyntax>() is
            {
                NameColon.Name.Identifier: { IsMissing: false, ContainsDiagnostics: false },
                Pattern: RecursivePatternSyntax
                {
                    Type: null,
                    Designation: null,
                    PositionalPatternClause: null,
                    PropertyPatternClause.Subpatterns: [{ NameColon.Name.Identifier: { IsMissing: false, ContainsDiagnostics: false } }],
                },
            };

    /// <summary>Rewrites <c>{ A: { B: v } }</c> as <c>{ A.B: v }</c>.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The subpattern to replace, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                .FirstAncestorOrSelf<SubpatternSyntax>() is not { NameColon: { } outerName } outer
            || outer.Pattern is not RecursivePatternSyntax
            {
                Type: null,
                Designation: null,
                PositionalPatternClause: null,
                PropertyPatternClause.Subpatterns: [{ NameColon: { } innerName } inner],
            })
        {
            return null;
        }

        var path = SyntaxFactory.ParseExpression($"{outerName.Name.WithoutTrivia()}.{innerName.Name.WithoutTrivia()}");
        if (path.ContainsDiagnostics)
        {
            return null;
        }

        var flattened = SyntaxFactory.Subpattern(
            SyntaxFactory.ExpressionColon(
                path.WithLeadingTrivia(outer.GetLeadingTrivia()),
                SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.ColonToken, SyntaxFactory.TriviaList(SyntaxFactory.Space))),
            inner.Pattern.WithoutLeadingTrivia().WithTrailingTrivia(outer.GetTrailingTrivia()));

        return new NodeReplacement(outer, flattened);
    }
}
