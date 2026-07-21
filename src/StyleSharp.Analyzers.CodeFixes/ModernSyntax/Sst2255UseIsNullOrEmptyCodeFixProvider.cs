// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a hand-written null-or-empty string test as <c>string.IsNullOrEmpty(value)</c> (SST2255),
/// or <c>!string.IsNullOrEmpty(value)</c> for the negated conjunction. The tested value node and the
/// whole expression's trivia carry through unchanged.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2255UseIsNullOrEmptyCodeFixProvider))]
[Shared]
public sealed class Sst2255UseIsNullOrEmptyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UseIsNullOrEmpty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use string.IsNullOrEmpty", nameof(Sst2255UseIsNullOrEmptyCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported disjunction/conjunction and rewrites it to the helper call.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is BinaryExpressionSyntax binary
                && (binary.IsKind(SyntaxKind.LogicalOrExpression) || binary.IsKind(SyntaxKind.LogicalAndExpression)))
            {
                return Sst2255UseIsNullOrEmptyAnalyzer.TryMatch(binary, out var value, out var negated)
                    ? new NodeReplacement(binary, Build(binary, value, negated))
                    : null;
            }
        }

        return null;
    }

    /// <summary>Builds the <c>string.IsNullOrEmpty</c> call replacing the reported expression.</summary>
    /// <param name="binary">The reported expression, used for its trivia.</param>
    /// <param name="value">The tested value.</param>
    /// <param name="negated">Whether to negate the call for the conjunction form.</param>
    /// <returns>The replacement expression.</returns>
    private static ExpressionSyntax Build(BinaryExpressionSyntax binary, ExpressionSyntax value, bool negated)
    {
        var invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                SyntaxFactory.IdentifierName("IsNullOrEmpty")),
            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(value.WithoutTrivia()))));

        ExpressionSyntax result = negated
            ? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, invocation)
            : invocation;

        return result.WithTriviaFrom(binary);
    }
}
