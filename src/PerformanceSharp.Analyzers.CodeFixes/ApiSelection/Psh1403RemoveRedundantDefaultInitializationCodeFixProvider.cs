// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>Removes a field initializer that restates the type's default value (PSH1403).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1403RemoveRedundantDefaultInitializationCodeFixProvider))]
[Shared]
public sealed class Psh1403RemoveRedundantDefaultInitializationCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(ReportedNode.Ancestor<VariableDeclaratorSyntax>, static (current, _) => Rewrite((VariableDeclaratorSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.RemoveRedundantDefaultInitialization.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Remove the redundant initializer",
            nameof(Psh1403RemoveRedundantDefaultInitializationCodeFixProvider),
            ReportedNode.Ancestor<VariableDeclaratorSyntax>,
            Rewrite);

    /// <summary>Drops the initializer while keeping the declarator's trailing trivia.</summary>
    /// <param name="declarator">The variable declarator to rewrite.</param>
    /// <returns>The rewritten declarator.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VariableDeclaratorSyntax Rewrite(VariableDeclaratorSyntax declarator) =>
        declarator.Update(
            declarator.ArgumentList is null
                ? declarator.Identifier.WithTrailingTrivia(declarator.GetTrailingTrivia())
                : declarator.Identifier,
            declarator.ArgumentList?.WithTrailingTrivia(declarator.GetTrailingTrivia()),
            initializer: null);
}
