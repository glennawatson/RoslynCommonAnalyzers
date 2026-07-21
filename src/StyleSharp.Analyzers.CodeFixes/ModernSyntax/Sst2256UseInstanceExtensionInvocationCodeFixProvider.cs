// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a static-form extension call as an instance-form call (SST2256): the first argument becomes the
/// receiver and the remaining arguments stay in order. The rewrite is re-derived and re-bound so it only
/// replaces the call when the instance form resolves to the same method.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2256UseInstanceExtensionInvocationCodeFixProvider))]
[Shared]
public sealed class Sst2256UseInstanceExtensionInvocationCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UseInstanceExtensionInvocation.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Call the extension method in instance form", nameof(Sst2256UseInstanceExtensionInvocationCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported call and rewrites it to instance form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no safe rewrite exists.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        var invocation = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null
            || !Sst2256UseInstanceExtensionInvocationAnalyzer.TryBuildInstanceForm(invocation, model, CancellationToken.None, out var instanceForm, out _))
        {
            return null;
        }

        return new NodeReplacement(invocation, instanceForm.WithTriviaFrom(invocation));
    }
}
