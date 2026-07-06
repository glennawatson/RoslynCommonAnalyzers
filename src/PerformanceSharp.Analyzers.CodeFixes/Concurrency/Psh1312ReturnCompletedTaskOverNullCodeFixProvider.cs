// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces a null/default returned in place of a task (PSH1312) with the analyzer's suggested
/// completed-task expression: <c>Task.CompletedTask</c> for <c>Task</c> and
/// <c>Task.FromResult&lt;T&gt;(default)</c> for <c>Task&lt;T&gt;</c>. The replacement text rides
/// on the diagnostic's properties, so the fix stays purely syntactic and always matches the
/// reported message.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1312ReturnCompletedTaskOverNullCodeFixProvider))]
[Shared]
public sealed class Psh1312ReturnCompletedTaskOverNullCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.ReturnCompletedTaskOverNull.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Return a completed task", nameof(Psh1312ReturnCompletedTaskOverNullCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported returned expression with the suggested completed-task expression.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="returned">The reported null/default expression.</param>
    /// <param name="replacementText">The replacement expression text suggested by the analyzer.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ExpressionSyntax returned, string replacementText)
        => document.WithSyntaxRoot(root.ReplaceNode(returned, CreateReplacement(returned, replacementText)));

    /// <summary>Resolves the reported returned expression and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => diagnostic.Properties.TryGetValue(Psh1312ReturnCompletedTaskOverNullAnalyzer.ReplacementKey, out var replacementText)
            && replacementText is { Length: > 0 }
            && root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is ExpressionSyntax returned
            && Psh1312ReturnCompletedTaskOverNullAnalyzer.IsNullOrDefaultShape(returned)
            ? new NodeReplacement(returned, CreateReplacement(returned, replacementText))
            : null;

    /// <summary>Parses the analyzer's replacement text, carrying over the original expression's trivia.</summary>
    /// <param name="returned">The reported null/default expression.</param>
    /// <param name="replacementText">The replacement expression text suggested by the analyzer.</param>
    /// <returns>The replacement expression annotated for formatting.</returns>
    private static ExpressionSyntax CreateReplacement(ExpressionSyntax returned, string replacementText)
        => SyntaxFactory.ParseExpression(replacementText)
            .WithTriviaFrom(returned)
            .WithAdditionalAnnotations(Formatter.Annotation);
}
