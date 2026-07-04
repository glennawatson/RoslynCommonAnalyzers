// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported <c>Task.FromResult(x)</c> call to <c>Task.CompletedTask</c> (PSH1308).
/// The original receiver expression carries over unchanged, so an alias or a fully qualified
/// spelling stays as the author wrote it. The dropped argument is a value the caller can never
/// observe; the analyzer only reports calls consumed as the non-generic task.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1308CompletedTaskCodeFixProvider))]
[Shared]
public sealed class Psh1308CompletedTaskCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.UseCompletedTask.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use Task.CompletedTask", nameof(Psh1308CompletedTaskCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported FromResult invocation and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation
            && Psh1308CompletedTaskAnalyzer.IsTaskFromResultShape(invocation)
            ? new NodeReplacement(invocation, Rewrite(invocation))
            : null;

    /// <summary>Builds the <c>Task.CompletedTask</c> replacement, reusing the original receiver spelling.</summary>
    /// <param name="invocation">The FromResult invocation to rewrite.</param>
    /// <returns>The property access replacement carrying the invocation's trivia.</returns>
    private static MemberAccessExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
    {
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                access.Expression.WithoutTrivia(),
                SyntaxFactory.IdentifierName(Psh1308CompletedTaskAnalyzer.CompletedTaskPropertyName))
            .WithTriviaFrom(invocation);
    }
}
