// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Replaces a parameter-naming string literal with a <c>nameof</c> expression (SST1415).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1415UseNameofCodeFixProvider))]
[Shared]
public sealed class Sst1415UseNameofCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(FindLiteral, static (current, _) => BuildNameof((LiteralExpressionSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.UseNameofForParameter.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            static literal => $"Use 'nameof({literal.Token.ValueText})'",
            nameof(Sst1415UseNameofCodeFixProvider),
            FindLiteral,
            Replace);

    /// <summary>Replaces the string literal with the corresponding <c>nameof</c> expression.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="literal">The parameter-name literal.</param>
    /// <returns>The updated document.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Document Replace(Document document, SyntaxNode root, LiteralExpressionSyntax literal) =>
        document.WithSyntaxRoot(root.ReplaceNode(literal, BuildNameof(literal)));

    /// <summary>Resolves the diagnostic to the parameter-name literal it was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The literal, or <see langword="null"/> when the shape no longer matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LiteralExpressionSyntax? FindLiteral(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) as LiteralExpressionSyntax;

    /// <summary>Builds the <c>nameof</c> expression that names the literal's parameter.</summary>
    /// <param name="literal">The parameter-name literal.</param>
    /// <returns>The <c>nameof</c> expression, carrying the literal's trivia.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExpressionSyntax BuildNameof(LiteralExpressionSyntax literal) =>
        SyntaxFactory.ParseExpression($"nameof({literal.Token.ValueText})").WithTriviaFrom(literal);
}
