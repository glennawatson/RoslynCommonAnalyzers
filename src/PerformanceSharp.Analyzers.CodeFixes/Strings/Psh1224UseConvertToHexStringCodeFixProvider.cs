// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces the hand-rolled hex chain (PSH1224) with the one call that does the same thing:
/// <c>BitConverter.ToString(bytes).Replace("-", "")</c> becomes <c>Convert.ToHexString(bytes)</c>. The
/// byte arguments carry over untouched, so the offset-and-count form is rewritten as readily as the
/// whole-array one.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1224UseConvertToHexStringCodeFixProvider))]
[Shared]
public sealed class Psh1224UseConvertToHexStringCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseConvertToHexString.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Convert to hex in one call",
            nameof(Psh1224UseConvertToHexStringCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported chain and builds the one-call rewrite.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    /// <remarks>
    /// The unqualified <c>Convert</c> is preferred and the fully qualified spelling is the fallback, so
    /// a file without <c>using System;</c> still gets a fix that compiles. Whichever is chosen is bound
    /// before the fix is offered.
    /// </remarks>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not InvocationExpressionSyntax invocation
            || Psh1224UseConvertToHexStringAnalyzer.TryGetSeparatedHexCall(invocation) is not { } separatedHex)
        {
            return null;
        }

        var simple = Psh1224UseConvertToHexStringAnalyzer.BuildHexCall(separatedHex, Psh1224UseConvertToHexStringAnalyzer.ConvertTypeName);
        if (Psh1224UseConvertToHexStringAnalyzer.RewriteBindsToHexString(model, invocation.SpanStart, simple))
        {
            return new NodeReplacement(invocation, simple.WithTriviaFrom(invocation));
        }

        var qualified = Psh1224UseConvertToHexStringAnalyzer.BuildHexCall(separatedHex, Psh1224UseConvertToHexStringAnalyzer.QualifiedConvert);
        return Psh1224UseConvertToHexStringAnalyzer.RewriteBindsToHexString(model, invocation.SpanStart, qualified)
            ? new NodeReplacement(invocation, qualified.WithTriviaFrom(invocation))
            : null;
    }
}
