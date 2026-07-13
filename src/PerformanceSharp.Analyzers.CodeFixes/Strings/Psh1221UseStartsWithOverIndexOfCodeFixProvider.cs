// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported search-for-position-zero (PSH1221) into the prefix test it was asking for:
/// <c>text.IndexOf(v) == 0</c> becomes <c>text.StartsWith(v)</c>, and <c>!= 0</c> becomes
/// <c>!text.StartsWith(v)</c>.
/// </summary>
/// <remarks>
/// The arguments move across untouched, which is the whole point: whatever comparison the
/// <c>IndexOf</c> call named — an ordinal <c>char</c>, an explicit <see cref="StringComparison"/>, or
/// the current culture by default — the <c>StartsWith</c> call names the same one. The rewrite is
/// bound before it is offered, so the overload it needs is known to exist on the target framework.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1221UseStartsWithOverIndexOfCodeFixProvider))]
[Shared]
public sealed class Psh1221UseStartsWithOverIndexOfCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseStartsWithOverIndexOf.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Test the prefix with StartsWith",
            nameof(Psh1221UseStartsWithOverIndexOfCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported comparison and builds its prefix test.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not BinaryExpressionSyntax comparison
            || Psh1221UseStartsWithOverIndexOfAnalyzer.TryGetIndexOfCall(comparison) is not { } indexOf
            || !BindsToStartsWith(model, comparison, indexOf))
        {
            return null;
        }

        var prefixTest = Psh1221UseStartsWithOverIndexOfAnalyzer.BuildPrefixTest(comparison, indexOf);
        return new NodeReplacement(comparison, prefixTest.WithTriviaFrom(comparison));
    }

    /// <summary>Confirms the renamed call still binds to a boolean <c>string.StartsWith</c>.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="comparison">The reported comparison.</param>
    /// <param name="indexOf">The <c>IndexOf</c> invocation.</param>
    /// <returns><see langword="true"/> when the rewrite compiles.</returns>
    private static bool BindsToStartsWith(SemanticModel model, BinaryExpressionSyntax comparison, InvocationExpressionSyntax indexOf)
    {
        var access = (MemberAccessExpressionSyntax)indexOf.Expression;
        var name = SyntaxFactory.IdentifierName(Psh1221UseStartsWithOverIndexOfAnalyzer.StartsWithMethodName);
        var rewritten = indexOf.WithExpression(access.WithName(name));
        return model.GetSpeculativeSymbolInfo(comparison.SpanStart, rewritten, SpeculativeBindingOption.BindAsExpression).Symbol
            is IMethodSymbol
            {
                Name: Psh1221UseStartsWithOverIndexOfAnalyzer.StartsWithMethodName,
                IsStatic: false,
                ReturnType.SpecialType: SpecialType.System_Boolean,
                ContainingType.SpecialType: SpecialType.System_String,
            };
    }
}
