// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Removes an empty finalizer (PSH1002).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1002EmptyFinalizerCodeFixProvider))]
[Shared]
public sealed class Psh1002EmptyFinalizerCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.RemoveEmptyFinalizer.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => RemoveNodeCodeFix.RegisterAsync(context, "Remove the empty finalizer", nameof(Psh1002EmptyFinalizerCodeFixProvider), RemoveNodeCodeFix.Ancestor<DestructorDeclarationSyntax>);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => RemoveNodeCodeFix.ApplyBatchEdit(editor, diagnostic, RemoveNodeCodeFix.Ancestor<DestructorDeclarationSyntax>);
}
