// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Puts a reversed equality assertion's arguments back in order (SST2502), so the constant becomes the
/// expected value and the computed value becomes the actual.
/// </summary>
/// <remarks>
/// Only the two argument expressions move; each argument keeps the trivia around it, so a call spread over
/// several lines keeps its shape. The two arguments already bind to the same expected/actual parameters
/// regardless of order, so the reordered call still compiles.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2502ReversedEqualityAssertionCodeFixProvider))]
[Shared]
public sealed class Sst2502ReversedEqualityAssertionCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(TestingRules.ReversedEqualityAssertion.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Put the expected value first",
            nameof(Sst2502ReversedEqualityAssertionCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported argument and swaps it with the expected position.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => SwappedArgumentCodeFix.TryBuildSwap(root, diagnostic, Sst2502ReversedEqualityAssertionAnalyzer.SwapWithKey);
}
