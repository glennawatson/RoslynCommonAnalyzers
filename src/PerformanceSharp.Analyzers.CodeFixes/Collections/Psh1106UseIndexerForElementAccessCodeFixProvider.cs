// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces an Enumerable element-access call with the receiver's indexer (PSH1106):
/// <c>x.First()</c> becomes <c>x[0]</c>, <c>x.ElementAt(i)</c> becomes <c>x[i]</c>, and
/// <c>x.Last()</c> becomes <c>x[x.Count - 1]</c> (or <c>x[x.Length - 1]</c>). The
/// <c>Last()</c> rewrite duplicates the receiver into the index expression, so it is
/// only offered when the receiver is side-effect-free to repeat — an identifier,
/// <c>this</c>, or a simple member-access chain; the analyzer still reports other
/// receivers, but no automatic fix is registered for them.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1106UseIndexerForElementAccessCodeFixProvider))]
[Shared]
public sealed class Psh1106UseIndexerForElementAccessCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.UseIndexerForElementAccess.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use the indexer", nameof(Psh1106UseIndexerForElementAccessCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported Enumerable call with the receiver's indexer form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The reported invocation.</param>
    /// <param name="countPropertyName">The count property used by the <c>Last()</c> rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation, string countPropertyName)
        => document.WithSyntaxRoot(root.ReplaceNode(invocation, CreateReplacement(invocation, countPropertyName)));

    /// <summary>Resolves the reported Enumerable call and builds its indexer replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>() is { Expression: MemberAccessExpressionSyntax } invocation
            && CanApply(invocation)
            ? new NodeReplacement(invocation, CreateReplacement(invocation, GetCountSourceName(diagnostic)))
            : null;

    /// <summary>Returns whether the fix can rewrite the invocation without duplicating a side effect.</summary>
    /// <param name="invocation">The reported invocation.</param>
    /// <returns><see langword="true"/> except for <c>Last()</c> on a receiver that is not safe to repeat.</returns>
    private static bool CanApply(InvocationExpressionSyntax invocation)
    {
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        return memberAccess.Name.Identifier.ValueText != "Last" || IsSimpleReceiver(memberAccess.Expression);
    }

    /// <summary>Builds the indexer replacement for the reported invocation.</summary>
    /// <param name="invocation">The reported invocation.</param>
    /// <param name="countPropertyName">The count property used by the <c>Last()</c> rewrite.</param>
    /// <returns>The element-access replacement expression.</returns>
    private static ElementAccessExpressionSyntax CreateReplacement(InvocationExpressionSyntax invocation, string countPropertyName)
    {
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var index = memberAccess.Name.Identifier.ValueText switch
        {
            "First" => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)),
            "Last" => CreateLastIndex(memberAccess.Expression, countPropertyName),
            _ => invocation.ArgumentList.Arguments[0].Expression
        };

        return SyntaxFactory.ElementAccessExpression(
                memberAccess.Expression.WithoutTrailingTrivia(),
                SyntaxFactory.BracketedArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(index))))
            .WithTriviaFrom(invocation);
    }

    /// <summary>Builds the <c>receiver.Count - 1</c> index expression for the <c>Last()</c> rewrite.</summary>
    /// <param name="receiver">The receiver expression to duplicate into the index.</param>
    /// <param name="countPropertyName">The receiver's count property name.</param>
    /// <returns>The last-element index expression.</returns>
    private static BinaryExpressionSyntax CreateLastIndex(ExpressionSyntax receiver, string countPropertyName)
        => SyntaxFactory.BinaryExpression(
            SyntaxKind.SubtractExpression,
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                receiver.WithoutTrivia(),
                SyntaxFactory.IdentifierName(countPropertyName)),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), SyntaxKind.MinusToken, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1)));

    /// <summary>Returns whether a receiver expression is side-effect-free to duplicate.</summary>
    /// <param name="expression">The receiver expression.</param>
    /// <returns><see langword="true"/> for identifiers, <c>this</c>, and simple member-access chains over them.</returns>
    private static bool IsSimpleReceiver(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierNameSyntax or ThisExpressionSyntax => true,
            MemberAccessExpressionSyntax access => access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                && access.Name is IdentifierNameSyntax
                && IsSimpleReceiver(access.Expression),
            _ => false
        };

    /// <summary>Reads the analyzer's count source property name from the diagnostic.</summary>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The count property name; only meaningful for <c>Last()</c> diagnostics.</returns>
    private static string GetCountSourceName(Diagnostic diagnostic)
        => diagnostic.Properties.TryGetValue(Psh1106UseIndexerForElementAccessAnalyzer.CountSourceKey, out var name) && name is not null
            ? name
            : "Count";
}
