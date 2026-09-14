// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Drops the <c>@</c> prefix from a verbatim string that needs no verbatim quoting (SST1184).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantVerbatimStringCodeFixProvider))]
[Shared]
public sealed class RedundantVerbatimStringCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantVerbatimString.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Remove the '@' prefix",
            nameof(RedundantVerbatimStringCodeFixProvider),
            ReportedNode.Is<LiteralExpressionSyntax>,
            TryRewrite);

    /// <summary>Resolves the reported verbatim literal and builds a regular literal holding the same text.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not LiteralExpressionSyntax literal)
        {
            return null;
        }

        var token = literal.Token;
        var regular = SyntaxFactory.Literal(token.LeadingTrivia, SyntaxFactory.Literal(token.ValueText).Text, token.ValueText, token.TrailingTrivia);
        return new NodeReplacement(literal, literal.WithToken(regular));
    }
}
