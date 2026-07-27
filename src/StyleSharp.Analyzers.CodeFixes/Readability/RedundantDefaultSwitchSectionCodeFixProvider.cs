// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant <c>default:</c> switch section that only breaks (SST1179).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantDefaultSwitchSectionCodeFixProvider))]
[Shared]
public sealed class RedundantDefaultSwitchSectionCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantDefaultSwitchSection.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => RemoveNodeCodeFix.RegisterAsync(context, "Remove the redundant 'default' section", nameof(RedundantDefaultSwitchSectionCodeFixProvider), RemoveNodeCodeFix.Ancestor<SwitchSectionSyntax>);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => RemoveNodeCodeFix.ApplyBatchEdit(editor, diagnostic, RemoveNodeCodeFix.Ancestor<SwitchSectionSyntax>);
}
