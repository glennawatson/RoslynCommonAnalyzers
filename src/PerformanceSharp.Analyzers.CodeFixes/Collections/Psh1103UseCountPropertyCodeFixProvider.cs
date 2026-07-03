// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces a parameterless Enumerable <c>Count()</c>/<c>Any()</c> call with the
/// receiver's own count property (PSH1103): <c>x.Count()</c> becomes <c>x.Count</c> (or
/// <c>x.Length</c>), <c>x.Any()</c> becomes <c>x.Count &gt; 0</c>, and <c>!x.Any()</c>
/// becomes <c>x.Count == 0</c>. Comparisons are parenthesized when the rewritten node
/// sits inside a larger expression.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1103UseCountPropertyCodeFixProvider))]
[Shared]
public sealed class Psh1103UseCountPropertyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.UseCountProperty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { Expression: MemberAccessExpressionSyntax } invocation)
            {
                continue;
            }

            var propertyName = GetPropertyName(diagnostic);
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Use '{propertyName}'",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, invocation, propertyName)),
                    equivalenceKey: nameof(Psh1103UseCountPropertyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { Expression: MemberAccessExpressionSyntax } invocation)
        {
            return;
        }

        var (target, replacement) = CreateReplacement(invocation, GetPropertyName(diagnostic));
        editor.ReplaceNode(target, replacement);
    }

    /// <summary>Replaces the reported Enumerable call with the receiver's count property form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The reported invocation.</param>
    /// <param name="propertyName">The count property name suggested by the analyzer.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation, string propertyName)
    {
        var (target, replacement) = CreateReplacement(invocation, propertyName);
        return document.WithSyntaxRoot(root.ReplaceNode(target, replacement));
    }

    /// <summary>Computes the node to replace and its property-read replacement.</summary>
    /// <param name="invocation">The reported invocation.</param>
    /// <param name="propertyName">The count property name suggested by the analyzer.</param>
    /// <returns>The replaced node (the invocation, or its enclosing logical-not) and the replacement expression.</returns>
    private static (SyntaxNode Target, ExpressionSyntax Replacement) CreateReplacement(InvocationExpressionSyntax invocation, string propertyName)
    {
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var propertyAccess = memberAccess.WithName(SyntaxFactory.IdentifierName(propertyName));
        if (memberAccess.Name.Identifier.ValueText == "Count")
        {
            return (invocation, propertyAccess.WithTriviaFrom(invocation));
        }

        if (invocation.Parent is PrefixUnaryExpressionSyntax unary
            && unary.IsKind(SyntaxKind.LogicalNotExpression)
            && unary.Operand == invocation)
        {
            var equality = CreateComparison(propertyAccess, SyntaxKind.EqualsExpression, SyntaxKind.EqualsEqualsToken);
            return (unary, ParenthesizeInsideExpression(equality, unary).WithTriviaFrom(unary));
        }

        var comparison = CreateComparison(propertyAccess, SyntaxKind.GreaterThanExpression, SyntaxKind.GreaterThanToken);
        return (invocation, ParenthesizeInsideExpression(comparison, invocation).WithTriviaFrom(invocation));
    }

    /// <summary>Builds a zero comparison against the receiver's count property with conventional spacing.</summary>
    /// <param name="propertyAccess">The receiver count property access.</param>
    /// <param name="expressionKind">The binary expression kind.</param>
    /// <param name="operatorKind">The binary operator token kind.</param>
    /// <returns>The comparison expression.</returns>
    private static BinaryExpressionSyntax CreateComparison(ExpressionSyntax propertyAccess, SyntaxKind expressionKind, SyntaxKind operatorKind)
        => SyntaxFactory.BinaryExpression(
            expressionKind,
            propertyAccess.WithoutTrivia(),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), operatorKind, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)));

    /// <summary>Parenthesizes a comparison when the replaced node sits inside a larger expression.</summary>
    /// <param name="comparison">The replacement comparison.</param>
    /// <param name="target">The node being replaced.</param>
    /// <returns>The comparison, parenthesized when the target's parent is an expression.</returns>
    private static ExpressionSyntax ParenthesizeInsideExpression(ExpressionSyntax comparison, SyntaxNode target)
        => target.Parent is ExpressionSyntax ? SyntaxFactory.ParenthesizedExpression(comparison) : comparison;

    /// <summary>Reads the analyzer's suggested count property name from the diagnostic.</summary>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The suggested property name.</returns>
    private static string GetPropertyName(Diagnostic diagnostic)
        => diagnostic.Properties.TryGetValue(Psh1103UseCountPropertyAnalyzer.PropertyNameKey, out var name) && name is not null
            ? name
            : "Count";
}
