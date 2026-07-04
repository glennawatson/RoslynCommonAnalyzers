// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Renames a reported <c>Substring</c> call to <c>AsSpan</c> (PSH1212). The start and length
/// arguments carry over unchanged — the AsSpan overloads mirror Substring's — and overload
/// resolution then picks the span-taking consumer the analyzer proved exists.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1212AsSpanOverSubstringCodeFixProvider))]
[Shared]
public sealed class Psh1212AsSpanOverSubstringCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseAsSpanOverSubstring.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Slice with AsSpan", nameof(Psh1212AsSpanOverSubstringCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported Substring call and builds its AsSpan rename.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is InvocationExpressionSyntax invocation
            && Psh1212AsSpanOverSubstringAnalyzer.IsSubstringArgumentShape(invocation)
            && ((MemberAccessExpressionSyntax)invocation.Expression).Name is { } name
            ? new NodeReplacement(
                name,
                SyntaxFactory.IdentifierName(Psh1212AsSpanOverSubstringAnalyzer.AsSpanMethodName).WithTriviaFrom(name))
            : null;
}
