// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported emptiness comparison of an Enumerable <c>Count()</c> result to the
/// short-circuiting <c>Any()</c> form (PSH1119): <c>xs.Count() &gt; 0</c> becomes
/// <c>xs.Any()</c> and <c>xs.Count() == 0</c> becomes <c>!xs.Any()</c>, carrying a predicate
/// argument over unchanged. No parenthesization is needed — a logical-not binds tighter than
/// any binary operator, so the replacement composes anywhere the comparison was legal.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1119UseAnyOverCountCodeFixProvider))]
[Shared]
public sealed class Psh1119UseAnyOverCountCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.UseAnyOverCount.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use Any()", nameof(Psh1119UseAnyOverCountCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported comparison with its Any() form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="comparison">The comparison expression to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BinaryExpressionSyntax comparison)
        => TryGetReplacement(comparison, out var replacement)
            ? document.WithSyntaxRoot(root.ReplaceNode(comparison, replacement!))
            : document;

    /// <summary>Resolves the reported comparison and builds its Any() replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is BinaryExpressionSyntax binary
            && TryGetReplacement(binary, out var replacement)
            ? new NodeReplacement(binary, replacement!)
            : null;

    /// <summary>Builds the Any() replacement for a reported comparison.</summary>
    /// <param name="binary">The comparison expression to rewrite.</param>
    /// <param name="replacement">The replacement expression when the shape is recognized.</param>
    /// <returns><see langword="true"/> when a replacement was built.</returns>
    private static bool TryGetReplacement(BinaryExpressionSyntax binary, out ExpressionSyntax? replacement)
    {
        if (Psh1119UseAnyOverCountAnalyzer.TryGetComparisonShape(binary) is not { } shape)
        {
            replacement = null;
            return false;
        }

        var memberAccess = (MemberAccessExpressionSyntax)shape.Invocation.Expression;
        ExpressionSyntax result = shape.Invocation
            .WithExpression(memberAccess.WithName(SyntaxFactory.IdentifierName(Psh1119UseAnyOverCountAnalyzer.AnyMethodName)))
            .WithoutTrivia();
        if (!shape.HasElements)
        {
            result = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, result);
        }

        replacement = result.WithTriviaFrom(binary).WithAdditionalAnnotations(Formatter.Annotation);
        return true;
    }
}
