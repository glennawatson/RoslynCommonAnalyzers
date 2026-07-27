// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant public, parameterless, empty constructor (SST1433).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantConstructorCodeFixProvider))]
[Shared]
public sealed class RedundantConstructorCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.NoRedundantConstructor.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => RemoveNodeCodeFix.RegisterAsync(context, "Remove the redundant constructor", nameof(RedundantConstructorCodeFixProvider), RemoveNodeCodeFix.Ancestor<ConstructorDeclarationSyntax>);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => RemoveNodeCodeFix.ApplyBatchEdit(editor, diagnostic, RemoveNodeCodeFix.Ancestor<ConstructorDeclarationSyntax>);
}
