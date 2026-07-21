// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a local declaration or <c>foreach</c> variable between <c>var</c> and its explicit type (SST2271).
/// The direction follows the reported type node — a <c>var</c> node gains the inferred type name and an
/// explicit node becomes <c>var</c> — and the inferred type is re-resolved and its name re-bound before the
/// explicit form is offered.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2271VarStyleCodeFixProvider))]
[Shared]
public sealed class Sst2271VarStyleCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.NormalizeVarStyle.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Normalize the variable type style", nameof(Sst2271VarStyleCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported type node and flips its var-versus-explicit spelling.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no safe rewrite exists.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not TypeSyntax typeSyntax)
        {
            return null;
        }

        if (!typeSyntax.IsVar)
        {
            return new NodeReplacement(typeSyntax, SyntaxFactory.IdentifierName("var").WithTriviaFrom(typeSyntax));
        }

        if (Sst2271VarStyleAnalyzer.ResolveVariableType(model, typeSyntax) is not { } resolvedType
            || !Sst2254ExplicitObjectCreationTypeAnalyzer.IsExpressibleTypeName(resolvedType))
        {
            return null;
        }

        var typeName = resolvedType.ToMinimalDisplayString(model, typeSyntax.SpanStart);
        if (!Sst2271VarStyleAnalyzer.TypeNameBindsTo(model, typeSyntax.SpanStart, typeName, resolvedType))
        {
            return null;
        }

        return new NodeReplacement(typeSyntax, SyntaxFactory.ParseTypeName(typeName).WithTriviaFrom(typeSyntax));
    }
}
