// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Renames a reported <c>Substring</c> slice to <c>AsSpan</c> (PSH1218) so the search runs over the
/// tail in place. The start argument carries over unchanged, and the search that follows resolves to
/// the <c>MemoryExtensions</c> overload the analyzer already bound.
/// </summary>
/// <remarks>
/// The fix is safe for a search whose numeric result is used as an index, not only for a boolean
/// test, because a span search reports its hit relative to the span — the same basis the substring
/// had. No offset is applied, and none is needed. Rewriting to <c>string.IndexOf(value, start)</c>
/// instead would have shifted every result by <c>start</c> and turned the not-found <c>-1</c> into
/// <c>-1 - start</c>, which is why that overload is not what this fix produces.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1218SearchWithStartIndexCodeFixProvider))]
[Shared]
public sealed class Psh1218SearchWithStartIndexCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.SearchWithStartIndex.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Search the tail with AsSpan",
            nameof(Psh1218SearchWithStartIndexCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported Substring slice and builds its AsSpan rename.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is InvocationExpressionSyntax slice
            && Psh1218SearchWithStartIndexAnalyzer.IsSubstringSliceShape(slice)
            && ((MemberAccessExpressionSyntax)slice.Expression).Name is { } name
            ? new NodeReplacement(
                name,
                SyntaxFactory.IdentifierName(Psh1218SearchWithStartIndexAnalyzer.AsSpanMethodName).WithTriviaFrom(name))
            : null;
}
