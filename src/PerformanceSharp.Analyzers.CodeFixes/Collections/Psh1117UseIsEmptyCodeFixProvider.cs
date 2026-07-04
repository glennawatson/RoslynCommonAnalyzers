// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported emptiness comparison to the receiver's <c>IsEmpty</c> property
/// (PSH1117): <c>x.Count == 0</c> becomes <c>x.IsEmpty</c> and <c>x.Length &gt; 0</c> becomes
/// <c>!x.IsEmpty</c>, keeping the receiver expression as written.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1117UseIsEmptyCodeFixProvider))]
[Shared]
public sealed class Psh1117UseIsEmptyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.UseIsEmpty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use IsEmpty", nameof(Psh1117UseIsEmptyCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported comparison and builds its IsEmpty replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not BinaryExpressionSyntax binary
            || Psh1117UseIsEmptyAnalyzer.TryGetEmptinessShape(binary) is not { } shape)
        {
            return null;
        }

        ExpressionSyntax replacement = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            shape.Count.Expression.WithoutTrivia(),
            SyntaxFactory.IdentifierName(Psh1117UseIsEmptyAnalyzer.IsEmptyPropertyName));
        if (!shape.IsEmpty)
        {
            replacement = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, replacement);
        }

        return new NodeReplacement(binary, replacement.WithTriviaFrom(binary));
    }
}
