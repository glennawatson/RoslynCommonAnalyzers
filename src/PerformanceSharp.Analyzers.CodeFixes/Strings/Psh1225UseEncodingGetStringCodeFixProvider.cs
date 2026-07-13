// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces a decode-then-copy (PSH1225) with the single call that produces the string directly:
/// <c>new string(encoding.GetChars(bytes))</c> becomes <c>encoding.GetString(bytes)</c>. The decoding
/// arguments carry over untouched, so the index-and-count form is rewritten as readily as the
/// whole-array one.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1225UseEncodingGetStringCodeFixProvider))]
[Shared]
public sealed class Psh1225UseEncodingGetStringCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseEncodingGetString.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Decode straight to a string",
            nameof(Psh1225UseEncodingGetStringCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported string creation and builds the direct decode.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not ObjectCreationExpressionSyntax creation
            || Psh1225UseEncodingGetStringAnalyzer.TryGetDecodeCall(creation) is not { } decodeCall
            || model.GetSymbolInfo(decodeCall).Symbol is not IMethodSymbol decode)
        {
            return null;
        }

        var rewritten = Psh1225UseEncodingGetStringAnalyzer.BuildGetString(decodeCall);
        return Psh1225UseEncodingGetStringAnalyzer.RewriteBindsToGetString(model, creation.SpanStart, rewritten, decode)
            ? new NodeReplacement(creation, rewritten.WithTriviaFrom(creation))
            : null;
    }
}
