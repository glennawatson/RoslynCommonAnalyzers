// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a self-assignment statement (SST1189).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SelfAssignmentCodeFixProvider))]
[Shared]
public sealed class SelfAssignmentCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TrySelect);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoSelfAssignment.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        RemoveNodeCodeFix.RegisterAsync(context, "Remove the self-assignment", nameof(SelfAssignmentCodeFixProvider), TrySelect);

    /// <summary>Resolves the statement holding the reported self-assignment.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The statement to remove, or <see langword="null"/> when the assignment is not a statement of its own.</returns>
    private static NodeRemoval? TrySelect(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is AssignmentExpressionSyntax { Parent: ExpressionStatementSyntax statement }
            ? new NodeRemoval(statement)
            : null;
}
