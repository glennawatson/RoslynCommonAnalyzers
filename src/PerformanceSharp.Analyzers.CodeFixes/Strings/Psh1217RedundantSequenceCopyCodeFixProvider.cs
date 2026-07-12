// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Removes a reported <c>ToCharArray()</c> or <c>ToArray()</c> copy (PSH1217), leaving its receiver
/// in place: the <c>foreach</c>, the <c>Length</c> read, the indexer, or the overload the analyzer
/// proved accepts the sequence itself then consumes the string or the span directly.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1217RedundantSequenceCopyCodeFixProvider))]
[Shared]
public sealed class Psh1217RedundantSequenceCopyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.RedundantSequenceCopy.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Remove the redundant copy", nameof(Psh1217RedundantSequenceCopyCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported copy and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is InvocationExpressionSyntax invocation
            && Psh1217RedundantSequenceCopyAnalyzer.IsSequenceCopyShape(invocation)
            ? new NodeReplacement(invocation, Rewrite(invocation))
            : null;

    /// <summary>Builds the replacement: the copied sequence by itself.</summary>
    /// <param name="invocation">The copying invocation.</param>
    /// <returns>The receiver expression carrying the invocation's trivia.</returns>
    private static ExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
        => ((MemberAccessExpressionSyntax)invocation.Expression).Expression.WithTriviaFrom(invocation);
}
