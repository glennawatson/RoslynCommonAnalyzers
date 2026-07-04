// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported <c>Thread.Sleep(x)</c> call to <c>await Task.Delay(x)</c> (PSH1303).
/// Both Sleep overloads (milliseconds and TimeSpan) have a matching Delay overload, so the
/// argument list carries over unchanged. The fix only applies where the call stands alone —
/// an expression statement or a lambda's expression body — and spells the task type fully
/// qualified when its simple name does not resolve at the call site.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1303NoThreadSleepInAsyncCodeFixProvider))]
[Shared]
public sealed class Psh1303NoThreadSleepInAsyncCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The simple name of the task type.</summary>
    private const string TaskTypeName = "Task";

    /// <summary>The name of the delay method the fix calls.</summary>
    private const string DelayMethodName = "Delay";

    /// <summary>The fully qualified task spelling used when the simple name does not resolve.</summary>
    private const string QualifiedTaskExpression = "global::System.Threading.Tasks.Task";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.NoThreadSleepInAsync.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Await Task.Delay instead", nameof(Psh1303NoThreadSleepInAsyncCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported sleep invocation and builds its awaited replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches or awaiting in place would not parse.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation
            && Psh1303NoThreadSleepInAsyncAnalyzer.IsThreadSleepShape(invocation)
            && invocation.Parent is ExpressionStatementSyntax or AnonymousFunctionExpressionSyntax
            ? new NodeReplacement(invocation, Rewrite(model, invocation))
            : null;

    /// <summary>Builds the <c>await Task.Delay(...)</c> replacement for a sleep invocation.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="invocation">The sleep invocation to rewrite.</param>
    /// <returns>The await expression carrying the original argument list and trivia.</returns>
    private static AwaitExpressionSyntax Rewrite(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        var delayCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                BuildTaskExpression(model, invocation),
                SyntaxFactory.IdentifierName(DelayMethodName)),
            invocation.ArgumentList.WithoutTrivia());

        return SyntaxFactory.AwaitExpression(
                SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                delayCall)
            .WithTriviaFrom(invocation);
    }

    /// <summary>Builds the task type expression, simple when the task's simple name resolves at the call site.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="invocation">The invocation whose position anchors the lookup.</param>
    /// <returns>The task type expression.</returns>
    private static ExpressionSyntax BuildTaskExpression(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(invocation.SpanStart, name: TaskTypeName))
        {
            if (candidate is INamedTypeSymbol { IsGenericType: false } named
                && named.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks")
            {
                return SyntaxFactory.IdentifierName(TaskTypeName);
            }
        }

        return SyntaxFactory.ParseExpression(QualifiedTaskExpression);
    }
}
