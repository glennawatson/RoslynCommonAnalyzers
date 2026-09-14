// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes an empty namespace declaration (SST1435).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyNamespaceCodeFixProvider))]
[Shared]
public sealed class EmptyNamespaceCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RemoveNodeCodeFix.Ancestor<BaseNamespaceDeclarationSyntax>);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.NoEmptyNamespace.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        RemoveNodeCodeFix.RegisterAsync(context, "Remove the empty namespace", nameof(EmptyNamespaceCodeFixProvider), RemoveNodeCodeFix.Ancestor<BaseNamespaceDeclarationSyntax>);
}
