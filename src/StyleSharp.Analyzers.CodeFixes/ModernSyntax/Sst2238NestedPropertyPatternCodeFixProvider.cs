// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Folds a nested property pattern into the extended property path it is equivalent to (SST2238).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2238NestedPropertyPatternCodeFixProvider))]
[Shared]
public sealed class Sst2238NestedPropertyPatternCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(ModernSyntaxRules.SimplifyNestedPropertyPattern.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Flatten the property pattern",
            nameof(Sst2238NestedPropertyPatternCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

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
