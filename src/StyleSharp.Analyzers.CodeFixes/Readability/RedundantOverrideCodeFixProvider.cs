// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Deletes an <c>override</c> that only forwards to its base member (SST1181).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantOverrideCodeFixProvider))]
[Shared]
public sealed class RedundantOverrideCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantOverride.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => RemoveNodeCodeFix.RegisterAsync(context, "Remove the redundant override", nameof(RedundantOverrideCodeFixProvider), RemoveNodeCodeFix.Ancestor<MemberDeclarationSyntax>);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => RemoveNodeCodeFix.ApplyBatchEdit(editor, diagnostic, RemoveNodeCodeFix.Ancestor<MemberDeclarationSyntax>);
}
