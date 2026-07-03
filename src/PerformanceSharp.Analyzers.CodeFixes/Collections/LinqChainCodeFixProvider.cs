// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reorders and merges LINQ chain links: moves a <c>Where</c> before a single-key
/// sort (PSH1107), renames a repeated sort to <c>ThenBy</c>/<c>ThenByDescending</c>
/// (PSH1108), and merges consecutive <c>Where</c> predicates into one call (PSH1109).
/// The PSH1107 fix is only offered when the sort is a single-key <c>OrderBy</c> or
/// <c>OrderByDescending</c>; moving a filter across a multi-key <c>ThenBy</c> chain
/// is left as a manual edit. The PSH1109 fix is skipped when the surviving parameter
/// name already occurs in the second predicate's body (shadowing risk).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LinqChainCodeFixProvider))]
[Shared]
public sealed class LinqChainCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>Initial capacity for the rename target list.</summary>
    private const int RenameTargetCapacity = 4;

    /// <summary>Initial capacity for the predicate body walk stack.</summary>
    private const int WalkStackCapacity = 8;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        CollectionRules.FilterBeforeSort.Id,
        CollectionRules.UseThenBy.Id,
        CollectionRules.MergeConsecutiveWhere.Id);

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

            var title = GetTitle(diagnostic.Id);
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

    /// <summary>Applies the chain fix for one diagnostic to a document.</summary>
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

    /// <summary>Gets the code-action title for one diagnostic id.</summary>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <returns>The code-action title.</returns>
    private static string GetTitle(string diagnosticId)
    {
        if (diagnosticId == CollectionRules.FilterBeforeSort.Id)
        {
            return "Filter before sorting";
        }

        return diagnosticId == CollectionRules.UseThenBy.Id
            ? "Refine the previous sort"
            : "Merge the Where predicates";
    }

    /// <summary>Creates the replacement node for one diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="oldNode">The node to replace.</param>
    /// <returns>The replacement node, or <see langword="null"/> when no edit applies.</returns>
    private static SyntaxNode? CreateEdit(SyntaxNode root, Diagnostic diagnostic, out SyntaxNode? oldNode)
    {
        oldNode = null;
        if (diagnostic.Id == CollectionRules.FilterBeforeSort.Id)
        {
            return CreateFilterBeforeSortFix(root, diagnostic.Location.SourceSpan, out oldNode);
        }

        return diagnostic.Id == CollectionRules.UseThenBy.Id
            ? CreateThenByFix(root, diagnostic.Location.SourceSpan, out oldNode)
            : CreateMergeWhereFix(root, diagnostic.Location.SourceSpan, out oldNode);
    }

    /// <summary>Creates a swapped <c>src.Where(p).OrderBy(k)</c> invocation for a single-key sort.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The <c>Where</c> invocation to replace.</param>
    /// <returns>The swapped invocation, or <see langword="null"/> for multi-key sort chains.</returns>
    private static InvocationExpressionSyntax? CreateFilterBeforeSortFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = null;
        var invocation = root.FindNode(span).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is not
            {
                ArgumentList: { Arguments.Count: 1 } filterArguments,
                Expression: MemberAccessExpressionSyntax
                {
                    Name: { } filterName,
                    Expression: InvocationExpressionSyntax
                    {
                        ArgumentList: { Arguments.Count: 1 } sortArguments,
                        Expression: MemberAccessExpressionSyntax { Name: { } sortName, Expression: { } source }
                    }
                }
            })
        {
            return null;
        }

        if (!IsSingleKeySortName(sortName.Identifier.ValueText) || IsSortInvocation(source))
        {
            return null;
        }

        oldNode = invocation;
        var filterInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, source.WithoutTrivia(), filterName.WithoutTrivia()),
            filterArguments.WithoutTrivia());
        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, filterInvocation, sortName.WithoutTrivia()),
            sortArguments.WithoutTrivia())
            .WithTriviaFrom(invocation);
    }

    /// <summary>Creates the <c>ThenBy</c>/<c>ThenByDescending</c> name for a repeated sort call.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The sort name to replace.</param>
    /// <returns>The refining method name.</returns>
    private static SimpleNameSyntax? CreateThenByFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = null;
        if (root.FindNode(span) is not SimpleNameSyntax name
            || !IsSingleKeySortName(name.Identifier.ValueText))
        {
            return null;
        }

        oldNode = name;
        var refiningName = name.Identifier.ValueText == "OrderBy" ? "ThenBy" : "ThenByDescending";
        return name is GenericNameSyntax generic
            ? SyntaxFactory.GenericName(SyntaxFactory.Identifier(refiningName))
                .WithTypeArgumentList(generic.TypeArgumentList)
                .WithTriviaFrom(generic)
            : SyntaxFactory.IdentifierName(refiningName).WithTriviaFrom(name);
    }

    /// <summary>Creates one merged <c>Where</c> call from two consecutive <c>Where</c> calls.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The outer <c>Where</c> invocation to replace.</param>
    /// <returns>The merged invocation, or <see langword="null"/> when merging risks capture.</returns>
    private static InvocationExpressionSyntax? CreateMergeWhereFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = null;
        var invocation = root.FindNode(span).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is not { Expression: MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax innerInvocation } }
            || !TryGetOneParameterLambda(invocation, out var secondLambda)
            || !TryGetOneParameterLambda(innerInvocation, out var firstLambda)
            || firstLambda.ExpressionBody is not { } firstBody
            || secondLambda.ExpressionBody is not { } secondBody)
        {
            return null;
        }

        var firstParameterName = GetLambdaParameterName(firstLambda);
        var secondParameterName = GetLambdaParameterName(secondLambda);
        if (firstParameterName != secondParameterName
            && !TryRenameParameter(secondBody, secondParameterName, firstParameterName, out secondBody))
        {
            return null;
        }

        oldNode = invocation;
        var merged = SyntaxFactory.BinaryExpression(
            SyntaxKind.LogicalAndExpression,
            Parenthesize(firstBody),
            SyntaxFactory.Token(SyntaxKind.AmpersandAmpersandToken)
                .WithLeadingTrivia(SyntaxFactory.Space)
                .WithTrailingTrivia(SyntaxFactory.Space),
            Parenthesize(secondBody));
        var mergedLambda = firstLambda is SimpleLambdaExpressionSyntax simple
            ? (LambdaExpressionSyntax)simple.WithExpressionBody(merged)
            : ((ParenthesizedLambdaExpressionSyntax)firstLambda).WithExpressionBody(merged);
        return innerInvocation
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(mergedLambda))))
            .WithTriviaFrom(invocation);
    }

    /// <summary>Renames references to the second lambda's parameter unless the new name risks capture.</summary>
    /// <param name="body">The second predicate's body.</param>
    /// <param name="oldName">The second lambda's parameter name.</param>
    /// <param name="newName">The surviving first parameter's name.</param>
    /// <param name="renamed">The rewritten body.</param>
    /// <returns><see langword="false"/> when <paramref name="newName"/> already occurs in the body.</returns>
    private static bool TryRenameParameter(ExpressionSyntax body, string oldName, string newName, out ExpressionSyntax renamed)
    {
        renamed = body;
        var targets = new List<SyntaxNode>(RenameTargetCapacity);
        if (!TryCollectRenameTargets(body, oldName, newName, targets))
        {
            return false;
        }

        if (targets.Count == 0)
        {
            return true;
        }

        renamed = body.ReplaceNodes(
            targets,
            (original, _) => SyntaxFactory.IdentifierName(newName).WithTriviaFrom(original));
        return true;
    }

    /// <summary>Collects parameter references to rename, rejecting bodies that already use the new name.</summary>
    /// <param name="body">The second predicate's body.</param>
    /// <param name="oldName">The second lambda's parameter name.</param>
    /// <param name="newName">The surviving first parameter's name.</param>
    /// <param name="targets">Receives the identifiers to rename.</param>
    /// <returns><see langword="false"/> when <paramref name="newName"/> already occurs in the body.</returns>
    private static bool TryCollectRenameTargets(ExpressionSyntax body, string oldName, string newName, List<SyntaxNode> targets)
    {
        var stack = new Stack<SyntaxNode>(WalkStackCapacity);
        stack.Push(body);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            var accepted = node is IdentifierNameSyntax identifier
                ? ClassifyIdentifier(identifier, oldName, newName, targets)
                : PushChildren(node, stack, newName);
            if (!accepted)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Classifies one identifier as a rename target, a conflict, or unrelated.</summary>
    /// <param name="identifier">The identifier to classify.</param>
    /// <param name="oldName">The second lambda's parameter name.</param>
    /// <param name="newName">The surviving first parameter's name.</param>
    /// <param name="targets">Receives the identifiers to rename.</param>
    /// <returns><see langword="false"/> when the identifier matches <paramref name="newName"/>.</returns>
    private static bool ClassifyIdentifier(IdentifierNameSyntax identifier, string oldName, string newName, List<SyntaxNode> targets)
    {
        var text = identifier.Identifier.ValueText;
        if (text == newName)
        {
            return false;
        }

        if (text != oldName || !IsRenameTargetIdentifier(identifier))
        {
            return true;
        }

        targets.Add(identifier);
        return true;
    }

    /// <summary>Pushes a node's children for the walk, rejecting declared names that match the new name.</summary>
    /// <param name="node">The node whose children to push.</param>
    /// <param name="stack">The walk stack.</param>
    /// <param name="newName">The surviving first parameter's name.</param>
    /// <returns><see langword="false"/> when a child identifier token matches <paramref name="newName"/>.</returns>
    private static bool PushChildren(SyntaxNode node, Stack<SyntaxNode> stack, string newName)
    {
        foreach (var child in node.ChildNodesAndTokens())
        {
            if (child.IsNode)
            {
                stack.Push(child.AsNode()!);
                continue;
            }

            var token = child.AsToken();
            if (token.IsKind(SyntaxKind.IdentifierToken) && token.ValueText == newName)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an identifier is a value reference rather than a member or argument name.</summary>
    /// <param name="identifier">The identifier to classify.</param>
    /// <returns><see langword="true"/> when renaming the identifier retargets the lambda parameter.</returns>
    private static bool IsRenameTargetIdentifier(IdentifierNameSyntax identifier)
        => identifier.Parent switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name != identifier,
            MemberBindingExpressionSyntax => false,
            QualifiedNameSyntax qualified => qualified.Right != identifier,
            AliasQualifiedNameSyntax alias => alias.Name != identifier,
            NameColonSyntax => false,
            NameEqualsSyntax => false,
            _ => true
        };

    /// <summary>Parenthesizes a predicate body unless it is already an atomic expression.</summary>
    /// <param name="expression">The predicate body.</param>
    /// <returns>The body ready to sit beside a logical-and operator.</returns>
    private static ExpressionSyntax Parenthesize(ExpressionSyntax expression)
        => expression is ParenthesizedExpressionSyntax
            or IdentifierNameSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or LiteralExpressionSyntax
            ? expression.WithoutTrivia()
            : SyntaxFactory.ParenthesizedExpression(expression.WithoutTrivia());

    /// <summary>Gets a lambda's single parameter name.</summary>
    /// <param name="lambda">The one-parameter lambda.</param>
    /// <returns>The parameter name.</returns>
    private static string GetLambdaParameterName(LambdaExpressionSyntax lambda)
        => lambda is SimpleLambdaExpressionSyntax simple
            ? simple.Parameter.Identifier.ValueText
            : ((ParenthesizedLambdaExpressionSyntax)lambda).ParameterList.Parameters[0].Identifier.ValueText;

    /// <summary>Gets the invocation's single one-parameter lambda argument.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="lambda">The lambda argument.</param>
    /// <returns><see langword="true"/> when the only argument is a lambda with exactly one parameter.</returns>
    private static bool TryGetOneParameterLambda(InvocationExpressionSyntax invocation, out LambdaExpressionSyntax lambda)
    {
        lambda = null!;
        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        switch (invocation.ArgumentList.Arguments[0].Expression)
        {
            case SimpleLambdaExpressionSyntax simple:
                {
                    lambda = simple;
                    return true;
                }

            case ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized:
                {
                    lambda = parenthesized;
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Returns whether the method name is a LINQ sort operator.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> for the four LINQ sort operators.</returns>
    private static bool IsSortMethodName(string name)
        => name is "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending";

    /// <summary>Returns whether the method name is a single-key LINQ sort operator.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> for <c>OrderBy</c> and <c>OrderByDescending</c>.</returns>
    private static bool IsSingleKeySortName(string name)
        => name is "OrderBy" or "OrderByDescending";

    /// <summary>Returns whether an expression is an invocation of a LINQ sort operator.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> when the expression is a sort invocation.</returns>
    private static bool IsSortInvocation(ExpressionSyntax expression)
        => expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name: { } name } }
            && IsSortMethodName(name.Identifier.ValueText);
}
