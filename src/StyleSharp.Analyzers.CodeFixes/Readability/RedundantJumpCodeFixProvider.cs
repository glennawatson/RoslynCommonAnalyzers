// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant trailing <c>return;</c> or <c>continue;</c> statement (SST1174).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantJumpCodeFixProvider))]
[Shared]
public sealed class RedundantJumpCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RemoveNodeCodeFix.Node<StatementSyntax>);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantJump.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        RemoveNodeCodeFix.RegisterAsync(context, "Remove the redundant statement", nameof(RedundantJumpCodeFixProvider), RemoveNodeCodeFix.Node<StatementSyntax>);
}
