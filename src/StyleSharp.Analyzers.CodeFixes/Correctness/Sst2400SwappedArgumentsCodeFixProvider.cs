// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Puts two transposed arguments back in the parameters' order (SST2400).
/// </summary>
/// <remarks>
/// Only the two reported positions move, and only their expressions do: each argument keeps the trivia that
/// was around it, so a call spread over several lines keeps its shape and only the two names change places.
/// The analyzer has already established that the two parameters share a type and a ref kind, so the
/// reordered call binds to the same method it did before.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2400SwappedArgumentsCodeFixProvider))]
[Shared]
public sealed class Sst2400SwappedArgumentsCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.SwappedArguments.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Swap the arguments into the parameter order",
            nameof(Sst2400SwappedArgumentsCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Applies one SST2400 swap for the reported argument.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original when the reported shape no longer matches.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
        => TryRewrite(root, diagnostic) is { } edit
            ? document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))
            : document;

    /// <summary>Resolves the reported argument and swaps it with the position it belongs in.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => SwappedArgumentCodeFix.TryBuildSwap(root, diagnostic, Sst2400SwappedArgumentsAnalyzer.SwapWithKey);
}
