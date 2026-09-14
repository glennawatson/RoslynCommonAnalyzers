// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
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
public sealed class Psh1312ReturnCompletedTaskOverNullCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.ReturnCompletedTaskOverNull.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Return a completed task", nameof(Psh1312ReturnCompletedTaskOverNullCodeFixProvider), CanRewrite, TryRewrite);

    /// <summary>Resolves the reported returned expression and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        diagnostic.Properties.TryGetValue(Psh1312ReturnCompletedTaskOverNullAnalyzer.ReplacementKey, out var replacementText)
            && replacementText is { Length: > 0 }
            && root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is ExpressionSyntax returned
            && Psh1312ReturnCompletedTaskOverNullAnalyzer.IsNullOrDefaultShape(returned)
            ? new NodeReplacement(returned, CreateReplacement(returned, replacementText))
            : null;

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        diagnostic.Properties.TryGetValue(Psh1312ReturnCompletedTaskOverNullAnalyzer.ReplacementKey, out var replacementText)
            && replacementText is { Length: > 0 }
            && root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)is ExpressionSyntax returned
            && Psh1312ReturnCompletedTaskOverNullAnalyzer.IsNullOrDefaultShape(returned);

    /// <summary>Parses the analyzer's replacement text, carrying over the original expression's trivia.</summary>
    /// <param name="returned">The reported null/default expression.</param>
    /// <param name="replacementText">The replacement expression text suggested by the analyzer.</param>
    /// <returns>The replacement expression annotated for formatting.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExpressionSyntax CreateReplacement(ExpressionSyntax returned, string replacementText) =>
        SyntaxFactory.ParseExpression(replacementText)
            .WithTriviaFrom(returned)
            .WithAdditionalAnnotations(Formatter.Annotation);
}
