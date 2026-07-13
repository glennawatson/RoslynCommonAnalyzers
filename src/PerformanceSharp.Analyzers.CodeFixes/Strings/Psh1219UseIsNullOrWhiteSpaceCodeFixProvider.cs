// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a trim-and-measure blank test to <c>string.IsNullOrWhiteSpace</c> (PSH1219):
/// <c>text.Trim().Length == 0</c>, <c>text.Trim() == ""</c> and
/// <c>string.IsNullOrEmpty(text.Trim())</c> all become <c>string.IsNullOrWhiteSpace(text)</c>, and the
/// tests that ask the opposite question — <c>Length != 0</c>, <c>!= ""</c> — become
/// <c>!string.IsNullOrWhiteSpace(text)</c> so the answer keeps its sense.
/// </summary>
/// <remarks>
/// The whole test is replaced, not just the <c>Trim</c> call, because the comparison itself disappears:
/// there is nothing left to compare once the question is asked directly. An invocation and a negated
/// invocation both bind tighter than anything that can surround an equality expression, so the
/// replacement never needs parentheses of its own.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1219UseIsNullOrWhiteSpaceCodeFixProvider))]
[Shared]
public sealed class Psh1219UseIsNullOrWhiteSpaceCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseIsNullOrWhiteSpace.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use string.IsNullOrWhiteSpace",
            nameof(Psh1219UseIsNullOrWhiteSpaceCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported blank test with its <c>string.IsNullOrWhiteSpace</c> form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="test">The reported test expression.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ExpressionSyntax test)
        => Psh1219UseIsNullOrWhiteSpaceAnalyzer.TryGetBlankTest(test, out var receiver, out var negated)
            ? document.WithSyntaxRoot(root.ReplaceNode(test, Rewrite(test, receiver!, negated)))
            : document;

    /// <summary>Resolves the reported test and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is ExpressionSyntax test
            && Psh1219UseIsNullOrWhiteSpaceAnalyzer.TryGetBlankTest(test, out var receiver, out var negated)
            ? new NodeReplacement(test, Rewrite(test, receiver!, negated))
            : null;

    /// <summary>Builds <c>string.IsNullOrWhiteSpace(receiver)</c>, negated when the test asked the opposite.</summary>
    /// <param name="test">The reported test expression, whose trivia the replacement inherits.</param>
    /// <param name="receiver">The string the <c>Trim</c> call was made on.</param>
    /// <param name="negated">Whether the test asked whether the string was <em>not</em> blank.</param>
    /// <returns>The replacement expression.</returns>
    private static ExpressionSyntax Rewrite(ExpressionSyntax test, ExpressionSyntax receiver, bool negated)
    {
        ExpressionSyntax call = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                SyntaxFactory.IdentifierName(Psh1219UseIsNullOrWhiteSpaceAnalyzer.IsNullOrWhiteSpaceMethodName)),
            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(receiver.WithoutTrivia()))));

        if (negated)
        {
            call = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, call);
        }

        return call.NormalizeWhitespace().WithTriviaFrom(test);
    }
}
