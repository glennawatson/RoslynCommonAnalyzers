// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces LINQ predicate calls with the receiver's native methods: renames
/// FirstOrDefault/Any/All to Find/Exists/TrueForAll on <c>List&lt;T&gt;</c> and
/// <c>ImmutableList&lt;T&gt;</c>, rewrites simple array receivers to the static
/// <c>System.Array</c> helpers (PSH1110), and collapses an equality-only Any
/// predicate into a Contains call (PSH1111). Array diagnostics reported without
/// the target-name property carry no fix because the receiver would have to move
/// into argument position.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CollectionNativeMethodCodeFixProvider))]
[Shared]
public sealed class CollectionNativeMethodCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The prefix distinguishing static <c>System.Array</c> targets from member renames.</summary>
    private const string ArrayTargetPrefix = "Array.";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        CollectionRules.UseCollectionNativePredicate.Id,
        CollectionRules.UseContainsForMembership.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, GetTitle, static diagnostic => diagnostic.Id, CreateEdit);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, CreateEdit);

    /// <summary>Applies the replacement for one diagnostic to a document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original when no edit applies.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.Apply(document, root, diagnostic, CreateEdit);

    /// <summary>Returns the action wording naming the native method the call becomes.</summary>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    /// <returns>The code action title.</returns>
    private static string GetTitle(Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, CollectionRules.UseContainsForMembership.Id, StringComparison.Ordinal)
            ? "Use Contains"
            : $"Use '{GetTargetName(diagnostic)}'";

    /// <summary>Creates the replacement node for one diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? CreateEdit(SyntaxNode root, Diagnostic diagnostic)
    {
        var replacement = string.Equals(diagnostic.Id, CollectionRules.UseContainsForMembership.Id, StringComparison.Ordinal)
            ? CreateContainsFix(root, diagnostic.Location.SourceSpan, out var oldNode)
            : CreateNativePredicateFix(root, diagnostic, out oldNode);

        return replacement is null || oldNode is null ? null : new NodeReplacement(oldNode, replacement);
    }

    /// <summary>Creates a <c>receiver.Contains(value)</c> replacement for an equality-only Any predicate.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The invocation to replace.</param>
    /// <returns>The Contains invocation.</returns>
    private static InvocationExpressionSyntax? CreateContainsFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = null;
        var invocation = root.FindNode(span).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is not { ArgumentList.Arguments.Count: 1, Expression: MemberAccessExpressionSyntax memberAccess }
            || !LinqCallSyntax.TryGetPredicateLambda(invocation.ArgumentList.Arguments[0].Expression, out var parameterName, out var expressionBody)
            || expressionBody is not BinaryExpressionSyntax equality
            || !LinqCallSyntax.TryGetComparedValue(equality, parameterName, out var value))
        {
            return null;
        }

        oldNode = invocation;
        return invocation.Update(
            memberAccess.WithName(SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(memberAccess.Name.GetLeadingTrivia(), "Contains", memberAccess.Name.GetTrailingTrivia()))),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.Token(SyntaxKind.OpenParenToken),
                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(value.WithoutTrivia())),
                SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.CloseParenToken, invocation.GetTrailingTrivia())));
    }

    /// <summary>Creates the native-predicate replacement stored in the diagnostic's target-name property.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="oldNode">The node to replace.</param>
    /// <returns>The replacement node, or <see langword="null"/> when the diagnostic carries no fix.</returns>
    private static SyntaxNode? CreateNativePredicateFix(SyntaxNode root, Diagnostic diagnostic, out SyntaxNode? oldNode)
    {
        oldNode = null;
        var target = GetTargetName(diagnostic);
        if (target.Length == 0)
        {
            return null;
        }

        var invocation = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is not { ArgumentList.Arguments.Count: 1, Expression: MemberAccessExpressionSyntax memberAccess })
        {
            return null;
        }

        if (!target.StartsWith(ArrayTargetPrefix, StringComparison.Ordinal))
        {
            oldNode = memberAccess.Name;
            return SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(memberAccess.Name.GetLeadingTrivia(), target, memberAccess.Name.GetTrailingTrivia()));
        }

        oldNode = invocation;
        return CreateArrayHelperInvocation(invocation, memberAccess, target.Substring(ArrayTargetPrefix.Length));
    }

    /// <summary>Creates a <c>System.Array.&lt;method&gt;(receiver, predicate)</c> invocation.</summary>
    /// <param name="invocation">The original invocation.</param>
    /// <param name="memberAccess">The original member access.</param>
    /// <param name="methodName">The static helper method name.</param>
    /// <returns>The static helper invocation.</returns>
    private static InvocationExpressionSyntax CreateArrayHelperInvocation(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        string methodName)
    {
        var helperAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("System"),
                SyntaxFactory.IdentifierName(nameof(Array))),
            SyntaxFactory.IdentifierName(methodName));
        var arguments = SyntaxFactory.SeparatedList(
            [
                SyntaxFactory.Argument(memberAccess.Expression.WithoutTrivia()),
                invocation.ArgumentList.Arguments[0].WithoutTrivia()
            ],
            [
                SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.CommaToken, SyntaxFactory.TriviaList(SyntaxFactory.Space))
            ]);
        return SyntaxFactory.InvocationExpression(
            helperAccess.WithLeadingTrivia(invocation.GetLeadingTrivia()),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.Token(SyntaxKind.OpenParenToken),
                arguments,
                SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.CloseParenToken, invocation.GetTrailingTrivia())));
    }

    /// <summary>Reads the analyzer's replacement target name from the diagnostic.</summary>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The target name, or an empty string when the diagnostic carries no fix.</returns>
    private static string GetTargetName(Diagnostic diagnostic) =>
        diagnostic.Properties.TryGetValue(CollectionNativeMethodAnalyzer.TargetNameKey, out var name) && name is not null
            ? name
            : string.Empty;
}
