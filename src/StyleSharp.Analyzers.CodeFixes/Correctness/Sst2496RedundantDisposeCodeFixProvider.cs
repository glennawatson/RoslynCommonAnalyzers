// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes the redundant explicit disposal reported by SST2496, leaving the enclosing <c>using</c> to
/// dispose the value. Only a call that is its own expression statement is removed; an explicit disposal
/// woven into a larger expression is left for the author.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2496RedundantDisposeCodeFixProvider))]
[Shared]
public sealed class Sst2496RedundantDisposeCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TrySelectRedundantStatement);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.RedundantDispose.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        RemoveNodeCodeFix.RegisterAsync(
            context,
            "Remove the redundant disposal",
            nameof(Sst2496RedundantDisposeCodeFixProvider),
            TrySelectRedundantStatement);

    /// <summary>Resolves the diagnostic to the expression statement that only makes the redundant call.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The statement removal, or <see langword="null"/> when the call is not its own statement.</returns>
    private static NodeRemoval? TrySelectRedundantStatement(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is { } invocation
            && invocation.Parent is ExpressionStatementSyntax statement
            ? new NodeRemoval(statement, SyntaxRemoveOptions.KeepUnbalancedDirectives)
            : null;
}
