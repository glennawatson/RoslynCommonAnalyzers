// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a single-bit <c>[Flags]</c> enum member value into the configured form (SST2272): a decimal
/// literal becomes <c>1 &lt;&lt; n</c>, and a <c>1 &lt;&lt; n</c> shift becomes its decimal literal. The
/// analyzer computes the replacement text from the member's resolved constant and stashes it on the
/// diagnostic, so the fix only reparses that text and preserves the value's trivia.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2272EnumFlagValueStyleCodeFixProvider))]
[Shared]
public sealed class Sst2272EnumFlagValueStyleCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.NormalizeEnumFlagValueStyle.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Normalize the flag value style", nameof(Sst2272EnumFlagValueStyleCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported value expression and reparses its stashed replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<EnumMemberDeclarationSyntax>()?.EqualsValue?.Value is not { } value
            || !diagnostic.Properties.TryGetValue(Sst2272EnumFlagValueStyleAnalyzer.ReplacementKey, out var replacementText)
            || replacementText is null)
        {
            return null;
        }

        var replacement = SyntaxFactory.ParseExpression(replacementText).WithTriviaFrom(value);
        return new NodeReplacement(value, replacement);
    }
}
