// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a general-purpose call to its purpose-built form (PSH1227):
/// <c>string.Compare(a, b, StringComparison.Ordinal)</c> becomes <c>string.CompareOrdinal(a, b)</c>
/// (dropping the comparison argument), and <c>Debug.Assert(false, message)</c> becomes
/// <c>Debug.Fail(message)</c> (dropping the condition). The receiver the author wrote is reused, so
/// the call's qualification is preserved.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1227PreferDedicatedCallCodeFixProvider))]
[Shared]
public sealed class Psh1227PreferDedicatedCallCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The index of the <c>StringComparison</c> argument the <c>CompareOrdinal</c> rewrite drops.</summary>
    private const int ComparisonArgumentIndex = 2;

    /// <summary>The index of the always-false condition argument the <c>Debug.Fail</c> rewrite drops.</summary>
    private const int ConditionArgumentIndex = 0;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.PreferDedicatedCall.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use the purpose-built call", nameof(Psh1227PreferDedicatedCallCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported invocation and builds its purpose-built replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return null;
        }

        if (Psh1227PreferDedicatedCallAnalyzer.IsCompareOrdinalShape(invocation))
        {
            return new NodeReplacement(invocation, RewriteCall(invocation, access, Psh1227PreferDedicatedCallAnalyzer.CompareOrdinalName, ComparisonArgumentIndex));
        }

        return Psh1227PreferDedicatedCallAnalyzer.IsDebugFailShape(invocation)
            ? new NodeReplacement(invocation, RewriteCall(invocation, access, Psh1227PreferDedicatedCallAnalyzer.FailName, ConditionArgumentIndex))
            : null;
    }

    /// <summary>Renames the invoked member and drops one argument, keeping the receiver and the rest in order.</summary>
    /// <param name="invocation">The reported invocation.</param>
    /// <param name="access">The invocation's member access.</param>
    /// <param name="replacementName">The purpose-built member name to call.</param>
    /// <param name="dropIndex">The index of the argument removed by the rewrite.</param>
    /// <returns>The rewritten invocation.</returns>
    private static InvocationExpressionSyntax RewriteCall(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access,
        string replacementName,
        int dropIndex)
    {
        var arguments = invocation.ArgumentList.Arguments;
        var parts = new List<SyntaxNodeOrToken>();
        for (var i = 0; i < arguments.Count; i++)
        {
            if (i == dropIndex)
            {
                continue;
            }

            if (parts.Count > 0)
            {
                parts.Add(CommaWithTrailingSpace());
            }

            parts.Add(arguments[i].WithoutTrivia());
        }

        var renamedAccess = access.WithName(SyntaxFactory.IdentifierName(replacementName));
        return invocation
            .WithExpression(renamedAccess)
            .WithArgumentList(invocation.ArgumentList.WithArguments(SyntaxFactory.SeparatedList<ArgumentSyntax>(parts)))
            .WithTriviaFrom(invocation);
    }

    /// <summary>Creates a comma token followed by a single space.</summary>
    /// <returns>The comma token.</returns>
    private static SyntaxToken CommaWithTrailingSpace()
        => SyntaxFactory.Token(default, SyntaxKind.CommaToken, SyntaxFactory.TriviaList(SyntaxFactory.Space));
}
