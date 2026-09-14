// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a composite <c>string.Format</c> call or a literal-plus-value concatenation as the
/// interpolated string that says the same thing (SST2249). The replacement is the node the analyzer
/// already proved compiles to the same value, carrying the original expression's trivia.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2249UseInterpolatedStringCodeFixProvider))]
[Shared]
public sealed class Sst2249UseInterpolatedStringCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UseInterpolatedString.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use an interpolated string",
            nameof(Sst2249UseInterpolatedStringCodeFixProvider),
            static (root, _, diagnostic) => CanRewrite(root, diagnostic),
            TryRewrite);

    /// <summary>Resolves the reported node and builds its interpolated-string replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, the interpolated string carrying the original's trivia, or <see langword="null"/> when no verified rewrite exists.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) switch
        {
            InvocationExpressionSyntax invocation when InterpolatedStringConversion.TryConvertFormat(model, invocation, CancellationToken.None) is { } formatted
                => new NodeReplacement(invocation, formatted.WithTriviaFrom(invocation)),
            InvocationExpressionSyntax invocation when InterpolatedStringConversion.TryConvertConcat(model, invocation, CancellationToken.None) is { } joined
                => new NodeReplacement(invocation, joined.WithTriviaFrom(invocation)),
            BinaryExpressionSyntax binary when InterpolatedStringConversion.TryConvertConcatenation(model, binary, CancellationToken.None) is { } concatenated
                => new NodeReplacement(binary, concatenated.WithTriviaFrom(binary)),
            _ => null,
        };

    /// <summary>Checks the reported expression's shape; the analyzer already verified its interpolation.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported expression still has a convertible shape.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) switch
        {
            InvocationExpressionSyntax invocation => InterpolatedStringConversion.IsFormatShape(invocation)
                || InterpolatedStringConversion.IsConcatShape(invocation),
            BinaryExpressionSyntax binary => InterpolatedStringConversion.IsConcatenationCandidate(binary),
            _ => false,
        };
}
