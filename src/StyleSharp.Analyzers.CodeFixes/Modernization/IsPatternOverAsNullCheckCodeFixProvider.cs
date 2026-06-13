// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites an <c>as</c> cast compared to null as an <c>is</c> type pattern (SST2005).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IsPatternOverAsNullCheckCodeFixProvider))]
[Shared]
public sealed class IsPatternOverAsNullCheckCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.UseIsPatternOverAsNullCheck.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not BinaryExpressionSyntax comparison
                || PatternMatchingAnalyzer.GetAsOperandComparedToNull(comparison) is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use an 'is' type pattern",
                    _ => Task.FromResult(Apply(context.Document, root, comparison)),
                    equivalenceKey: nameof(IsPatternOverAsNullCheckCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the comparison with <c>x is T</c> (for <c>!=</c>) or <c>x is not T</c> (for <c>==</c>).</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="comparison">The <c>as</c>-to-null comparison.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BinaryExpressionSyntax comparison)
    {
        var asExpression = PatternMatchingAnalyzer.GetAsOperandComparedToNull(comparison)!;
        var operand = asExpression.Left;
        var type = (TypeSyntax)asExpression.Right;

        ExpressionSyntax replacement = comparison.IsKind(SyntaxKind.NotEqualsExpression)
            ? PatternMatchingAnalyzer.BuildIsTypeTest(operand, type)
            : PatternMatchingAnalyzer.BuildIsNotPattern(operand, type);

        return document.WithSyntaxRoot(root.ReplaceNode(comparison, replacement.WithTriviaFrom(comparison)));
    }
}
