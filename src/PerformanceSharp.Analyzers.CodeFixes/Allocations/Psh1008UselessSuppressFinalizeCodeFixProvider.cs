// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Removes a useless <c>GC.SuppressFinalize(this)</c> call (PSH1008). The whole expression
/// statement is deleted; when the call is not a standalone statement no fix is offered.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1008UselessSuppressFinalizeCodeFixProvider))]
[Shared]
public sealed class Psh1008UselessSuppressFinalizeCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TrySelectStatement);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.UselessSuppressFinalize.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        RemoveNodeCodeFix.RegisterAsync(
            context,
            "Remove the SuppressFinalize call",
            nameof(Psh1008UselessSuppressFinalizeCodeFixProvider),
            TrySelectStatement);

    /// <summary>Resolves the diagnostic to the standalone SuppressFinalize statement to remove.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The statement removal, or <see langword="null"/> when the call is not a standalone statement.</returns>
    private static NodeRemoval? TrySelectStatement(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<InvocationExpressionSyntax>()?.Parent is ExpressionStatementSyntax statement
            ? new NodeRemoval(statement)
            : null;
}
