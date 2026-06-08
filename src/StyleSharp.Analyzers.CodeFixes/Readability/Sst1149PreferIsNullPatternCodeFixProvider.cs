// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Rewrites <c>x == null</c> / <c>x != null</c> as <c>x is null</c> / <c>x is not null</c> (SST1149).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1149PreferIsNullPatternCodeFixProvider))]
[Shared]
public sealed class Sst1149PreferIsNullPatternCodeFixProvider : CodeFixProvider
{
    /// <summary>Expression kinds whose text can appear before <c>is null</c> without added parentheses.</summary>
    private static readonly HashSet<SyntaxKind> PatternSafeKinds =
    [
        SyntaxKind.IdentifierName,
        SyntaxKind.SimpleMemberAccessExpression,
        SyntaxKind.InvocationExpression,
        SyntaxKind.ElementAccessExpression,
        SyntaxKind.ParenthesizedExpression,
        SyntaxKind.ThisExpression,
        SyntaxKind.BaseExpression,
        SyntaxKind.NullLiteralExpression,
        SyntaxKind.StringLiteralExpression,
        SyntaxKind.NumericLiteralExpression,
        SyntaxKind.CharacterLiteralExpression,
        SyntaxKind.TrueLiteralExpression,
        SyntaxKind.FalseLiteralExpression,
        SyntaxKind.ConditionalAccessExpression,
        SyntaxKind.ObjectCreationExpression,
        SyntaxKind.ImplicitObjectCreationExpression,
        SyntaxKind.DefaultExpression,
        SyntaxKind.TypeOfExpression,
        SyntaxKind.AwaitExpression,
        SyntaxKind.CastExpression
    ];

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.PreferIsNullPattern.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan) is not BinaryExpressionSyntax binary)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Use '{Sst1149PreferIsNullPatternAnalyzer.PatternText(binary.Kind())}'",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, binary)),
                    equivalenceKey: nameof(Sst1149PreferIsNullPatternCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the null comparison with the equivalent pattern expression.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="binary">The null comparison to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BinaryExpressionSyntax binary)
    {
        var replacement = Rewrite(binary).WithTriviaFrom(binary);
        return document.WithSyntaxRoot(root.ReplaceNode(binary, replacement));
    }

    /// <summary>Returns the rewritten null-pattern expression.</summary>
    /// <param name="binary">The null comparison.</param>
    /// <returns>The pattern form of the null comparison.</returns>
    private static IsPatternExpressionSyntax Rewrite(BinaryExpressionSyntax binary)
    {
        _ = Sst1149PreferIsNullPatternAnalyzer.TryGetNullComparison(binary, out var operand);
        var rewrittenOperand = ParenthesizeIfNeeded(operand!.WithoutTrivia());
        return SyntaxFactory.IsPatternExpression(rewrittenOperand, CreatePattern(binary.Kind()));
    }

    /// <summary>Returns the null-check pattern for the comparison kind.</summary>
    /// <param name="kind">The comparison kind.</param>
    /// <returns>The corresponding null-check pattern.</returns>
    private static PatternSyntax CreatePattern(SyntaxKind kind)
    {
        var nullPattern = SyntaxFactory.ConstantPattern(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));
        return kind == SyntaxKind.NotEqualsExpression ? SyntaxFactory.UnaryPattern(nullPattern) : nullPattern;
    }

    /// <summary>Parenthesizes an operand when <c>is null</c> would otherwise change or obscure the parse.</summary>
    /// <param name="operand">The expression operand.</param>
    /// <returns>The original operand or a parenthesized wrapper.</returns>
    private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax operand)
        => PatternSafeKinds.Contains(operand.Kind()) ? operand : SyntaxFactory.ParenthesizedExpression(operand);
}
