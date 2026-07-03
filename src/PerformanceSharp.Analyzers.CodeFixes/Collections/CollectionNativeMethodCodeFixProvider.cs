// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var replacement = CreateEdit(root, diagnostic, out var oldNode);
            if (replacement is null || oldNode is null)
            {
                continue;
            }

            var title = diagnostic.Id == CollectionRules.UseContainsForMembership.Id
                ? "Use Contains"
                : $"Use '{GetTargetName(diagnostic)}'";
            var document = context.Document;
            var currentDiagnostic = diagnostic;
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    _ => Task.FromResult(Apply(document, root, currentDiagnostic)),
                    equivalenceKey: diagnostic.Id),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        var replacement = CreateEdit(editor.OriginalRoot, diagnostic, out var oldNode);
        if (replacement is null || oldNode is null)
        {
            return;
        }

        editor.ReplaceNode(oldNode, replacement);
    }

    /// <summary>Applies the replacement for one diagnostic to a document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original when no edit applies.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        var replacement = CreateEdit(root, diagnostic, out var oldNode);
        return replacement is null || oldNode is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(oldNode, replacement));
    }

    /// <summary>Creates the replacement node for one diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="oldNode">The node to replace.</param>
    /// <returns>The replacement node, or <see langword="null"/>.</returns>
    private static SyntaxNode? CreateEdit(SyntaxNode root, Diagnostic diagnostic, out SyntaxNode? oldNode)
    {
        oldNode = null;
        return diagnostic.Id == CollectionRules.UseContainsForMembership.Id
            ? CreateContainsFix(root, diagnostic.Location.SourceSpan, out oldNode)
            : CreateNativePredicateFix(root, diagnostic, out oldNode);
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
            || !TryGetPredicateLambda(invocation.ArgumentList.Arguments[0].Expression, out var parameterName, out var expressionBody)
            || expressionBody is not BinaryExpressionSyntax equality
            || !TryGetComparedValue(equality, parameterName, out var value))
        {
            return null;
        }

        oldNode = invocation;
        return invocation
            .WithExpression(memberAccess.WithName(SyntaxFactory.IdentifierName("Contains").WithTriviaFrom(memberAccess.Name)))
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(value.WithoutTrivia()))))
            .WithTriviaFrom(invocation);
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
            return SyntaxFactory.IdentifierName(target).WithTriviaFrom(memberAccess.Name);
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
                SyntaxFactory.IdentifierName("Array")),
            SyntaxFactory.IdentifierName(methodName));
        var arguments = SyntaxFactory.SeparatedList(
            [
                SyntaxFactory.Argument(memberAccess.Expression.WithoutTrivia()),
                invocation.ArgumentList.Arguments[0].WithoutTrivia()
            ],
            [
                SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.CommaToken, SyntaxFactory.TriviaList(SyntaxFactory.Space))
            ]);
        return SyntaxFactory.InvocationExpression(helperAccess, SyntaxFactory.ArgumentList(arguments)).WithTriviaFrom(invocation);
    }

    /// <summary>Gets the parameter name and expression body of a one-parameter lambda argument.</summary>
    /// <param name="argument">The argument expression.</param>
    /// <param name="parameterName">The lambda parameter name.</param>
    /// <param name="expressionBody">The lambda expression body, or <see langword="null"/> for statement bodies.</param>
    /// <returns><see langword="true"/> when the argument is a one-parameter lambda.</returns>
    private static bool TryGetPredicateLambda(ExpressionSyntax argument, out string parameterName, out ExpressionSyntax? expressionBody)
    {
        switch (argument)
        {
            case SimpleLambdaExpressionSyntax simple:
                {
                    parameterName = simple.Parameter.Identifier.ValueText;
                    expressionBody = simple.ExpressionBody;
                    return true;
                }

            case ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized:
                {
                    parameterName = parenthesized.ParameterList.Parameters[0].Identifier.ValueText;
                    expressionBody = parenthesized.ExpressionBody;
                    return true;
                }

            default:
                {
                    parameterName = null!;
                    expressionBody = null;
                    return false;
                }
        }
    }

    /// <summary>Gets the non-parameter side of a <c>param == expr</c> or <c>expr == param</c> equality.</summary>
    /// <param name="equality">The equality expression.</param>
    /// <param name="parameterName">The lambda parameter name.</param>
    /// <param name="value">The compared value expression.</param>
    /// <returns><see langword="true"/> when one side is exactly the lambda parameter.</returns>
    private static bool TryGetComparedValue(BinaryExpressionSyntax equality, string parameterName, out ExpressionSyntax value)
    {
        if (equality.Left is IdentifierNameSyntax left && left.Identifier.ValueText == parameterName)
        {
            value = equality.Right;
            return true;
        }

        if (equality.Right is IdentifierNameSyntax right && right.Identifier.ValueText == parameterName)
        {
            value = equality.Left;
            return true;
        }

        value = null!;
        return false;
    }

    /// <summary>Reads the analyzer's replacement target name from the diagnostic.</summary>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The target name, or an empty string when the diagnostic carries no fix.</returns>
    private static string GetTargetName(Diagnostic diagnostic)
        => diagnostic.Properties.TryGetValue(CollectionNativeMethodAnalyzer.TargetNameKey, out var name) && name is not null
            ? name
            : string.Empty;
}
