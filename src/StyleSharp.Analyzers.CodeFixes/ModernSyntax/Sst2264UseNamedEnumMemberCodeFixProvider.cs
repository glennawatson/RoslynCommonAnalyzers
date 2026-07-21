// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a numeric enum cast to the named member (SST2264): <c>(RegexOptions)1</c> becomes
/// <c>RegexOptions.IgnoreCase</c>. The member name is re-derived and re-bound so the rewrite only replaces the
/// cast when <c>Type.Member</c> resolves to the same value.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2264UseNamedEnumMemberCodeFixProvider))]
[Shared]
public sealed class Sst2264UseNamedEnumMemberCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UseNamedEnumMember.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Name the enum member", nameof(Sst2264UseNamedEnumMemberCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported cast and rewrites it to the named enum member.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no safe rewrite exists.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        var cast = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<CastExpressionSyntax>();
        if (cast is null
            || !Sst2264UseNamedEnumMemberAnalyzer.TryGetNamedMemberAccess(cast, model, CancellationToken.None, out var memberAccessText))
        {
            return null;
        }

        var replacement = SyntaxFactory.ParseExpression(memberAccessText).WithTriviaFrom(cast);
        return new NodeReplacement(cast, replacement);
    }
}
