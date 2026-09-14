// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Replaces a numeric literal cast with the equivalent literal suffix (SST1139).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1139UseLiteralSuffixCodeFixProvider))]
[Shared]
public sealed class Sst1139UseLiteralSuffixCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseLiteralSuffix.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            static (root, diagnostic) => TryResolve(root, diagnostic, out _, out var suffix) ? $"Use '{suffix}' suffix" : null,
            static _ => nameof(Sst1139UseLiteralSuffixCodeFixProvider),
            TryRewrite);

    /// <summary>Resolves the reported cast and builds the suffixed literal that replaces it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryResolve(root, diagnostic, out var cast, out var suffix)
            ? new NodeReplacement(cast, CreateSuffixed(cast, suffix))
            : null;

    /// <summary>Resolves the reported cast and the literal suffix that expresses its type.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="cast">The cast expression.</param>
    /// <param name="suffix">The literal suffix to apply.</param>
    /// <returns><see langword="true"/> when the cast still has a suffix form.</returns>
    private static bool TryResolve(
        SyntaxNode root,
        Diagnostic diagnostic,
        [NotNullWhen(true)] out CastExpressionSyntax? cast,
        [NotNullWhen(true)] out string? suffix)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is CastExpressionSyntax reported
            && Sst1139UseLiteralSuffixAnalyzer.SuffixFor(reported) is { } reportedSuffix)
        {
            cast = reported;
            suffix = reportedSuffix;
            return true;
        }

        cast = null;
        suffix = null;
        return false;
    }

    /// <summary>Builds the suffixed literal that takes the cast's place and trivia.</summary>
    /// <param name="cast">The cast expression.</param>
    /// <param name="suffix">The literal suffix to apply.</param>
    /// <returns>The suffixed literal.</returns>
    private static LiteralExpressionSyntax CreateSuffixed(CastExpressionSyntax cast, string suffix)
    {
        var literal = (LiteralExpressionSyntax)Sst1139UseLiteralSuffixAnalyzer.Unwrap(cast.Expression);
        var parsed = (LiteralExpressionSyntax)SyntaxFactory.ParseExpression(literal.Token.Text + suffix);
        return parsed.Update(
            parsed.Token
                .WithLeadingTrivia(cast.GetLeadingTrivia())
                .WithTrailingTrivia(cast.GetTrailingTrivia()));
    }
}
