// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Unwraps a hand-written array at a <c>params</c> call site (PSH1018):
/// <c>Log(new object[] { a, b })</c> becomes <c>Log(a, b)</c>, and an empty array —
/// <c>Log(new object[0])</c>, <c>Log(Array.Empty&lt;object&gt;())</c> — becomes <c>Log()</c>, which
/// lets the compiler reuse the shared empty array. Arguments before the array are left untouched.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1018RedundantParamsArrayCodeFixProvider))]
[Shared]
public sealed class Psh1018RedundantParamsArrayCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.RedundantParamsArray.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Pass the arguments directly", nameof(Psh1018RedundantParamsArrayCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces one reported call with its unwrapped form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The reported call.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
        => document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation)));

    /// <summary>Resolves the reported call and builds its unwrapped replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        // The diagnostic sits on the array argument, which is itself an invocation for
        // Array.Empty<T>(), so the call being fixed is reached through the argument list rather
        // than by walking up to the nearest invocation.
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not { Parent: ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax invocation } })
        {
            return null;
        }

        return Psh1018RedundantParamsArrayAnalyzer.TryGetArrayArgument(invocation, out _)
            ? new NodeReplacement(invocation, Rewrite(invocation), RewriteCurrent)
            : null;
    }

    /// <summary>Rewrites the current call during batch FixAll composition.</summary>
    /// <param name="current">The current call node, possibly carrying nested edits.</param>
    /// <returns>The rewritten call, or the node unchanged when the shape no longer matches.</returns>
    private static SyntaxNode RewriteCurrent(SyntaxNode current)
        => current is InvocationExpressionSyntax invocation && Psh1018RedundantParamsArrayAnalyzer.TryGetArrayArgument(invocation, out _)
            ? Rewrite(invocation)
            : current;

    /// <summary>Builds the call with the array's elements passed directly.</summary>
    /// <param name="invocation">The reported call; callers must have validated the shape.</param>
    /// <returns>The rewritten call.</returns>
    private static InvocationExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
    {
        Psh1018RedundantParamsArrayAnalyzer.TryGetArrayArgument(invocation, out var arrayExpression);
        var elements = Psh1018RedundantParamsArrayAnalyzer.GetArrayElements(arrayExpression!);

        return invocation
            .WithArgumentList(Psh1018RedundantParamsArrayAnalyzer.BuildUnwrappedArgumentList(invocation.ArgumentList, elements))
            .WithTriviaFrom(invocation)
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }
}
