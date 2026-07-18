// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a same-value conditional chain as a switch expression (SST2246):
/// <c>x == 1 ? a : x == 2 ? b : c</c> becomes <c>x switch { 1 =&gt; a, 2 =&gt; b, _ =&gt; c }</c>. The
/// tested value, each result, and each constant are the original nodes; the analyzer has already
/// bound the rewrite, so the switch is only built when it keeps the chain's type.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2246ChainedConditionalToSwitchCodeFixProvider))]
[Shared]
public sealed class Sst2246ChainedConditionalToSwitchCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.ConvertChainedConditionalToSwitch.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Rewrite the conditional chain as a switch expression", nameof(Sst2246ChainedConditionalToSwitchCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported chain head and builds its switch-expression replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not ConditionalExpressionSyntax conditional
            || !Sst2246ChainedConditionalToSwitchAnalyzer.TryBuildSwitchExpression(conditional, model, CancellationToken.None, out var switchExpression))
        {
            return null;
        }

        var replacement = switchExpression
            .NormalizeWhitespace(elasticTrivia: true)
            .WithTriviaFrom(conditional)
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
        return new NodeReplacement(conditional, replacement);
    }
}
