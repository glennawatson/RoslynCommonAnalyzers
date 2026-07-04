// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Removes a reported intermediate <c>ToString()</c> call (PSH1211), leaving its receiver in
/// place. In an argument position overload resolution then picks the direct overload the
/// analyzer proved exists; in an interpolation hole the handler formats the value in place.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1211RemoveIntermediateToStringCodeFixProvider))]
[Shared]
public sealed class Psh1211RemoveIntermediateToStringCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.RemoveIntermediateToString.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Pass the value directly", nameof(Psh1211RemoveIntermediateToStringCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported ToString invocation and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is InvocationExpressionSyntax invocation
            && Psh1211RemoveIntermediateToStringAnalyzer.IsBareToStringShape(invocation)
            ? new NodeReplacement(invocation, Rewrite(invocation))
            : null;

    /// <summary>Builds the replacement: the ToString receiver by itself.</summary>
    /// <param name="invocation">The ToString invocation.</param>
    /// <returns>The receiver expression carrying the invocation's trivia.</returns>
    private static ExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
        => ((MemberAccessExpressionSyntax)invocation.Expression).Expression.WithTriviaFrom(invocation);
}
