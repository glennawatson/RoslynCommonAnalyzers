// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace StyleSharp.Analyzers;

/// <summary>
/// Changes an <c>async void</c> method or local function to return <c>Task</c> (SST1905), so callers can
/// await it and its exceptions become observable. The lambda form is not fixed — the delegate type is the
/// caller's to change, not this rewrite's.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1905AsyncVoidCodeFixProvider))]
[Shared]
public sealed class Sst1905AsyncVoidCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.DoNotUseAsyncVoid.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Return Task instead of void", nameof(Sst1905AsyncVoidCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported member's <c>void</c> return type and builds its <c>Task</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> for the lambda form.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var returnType = root.FindNode(diagnostic.Location.SourceSpan) switch
        {
            MethodDeclarationSyntax method => method.ReturnType,
            LocalFunctionStatementSyntax localFunction => localFunction.ReturnType,
            _ => null,
        };

        if (returnType is not PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword })
        {
            return null;
        }

        var task = SyntaxFactory.ParseTypeName("global::System.Threading.Tasks.Task")
            .WithTriviaFrom(returnType)
            .WithAdditionalAnnotations(Simplifier.Annotation, Formatter.Annotation);
        return new NodeReplacement(returnType, task);
    }
}
