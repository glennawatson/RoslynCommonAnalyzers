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
public sealed class Sst2271VarStyleCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.NormalizeVarStyle.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        SemanticModel? model = null;
        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan) is not TypeSyntax typeSyntax)
            {
                continue;
            }

            TypeSyntax? explicitType = null;
            if (typeSyntax.IsVar)
            {
                model ??= await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
                if (model is null || TryGetExplicitType(model, typeSyntax) is not { } candidate)
                {
                    continue;
                }

                explicitType = candidate;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Normalize the variable type style",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(
                        root.ReplaceNode(typeSyntax, Rewrite(typeSyntax, explicitType)))),
                    equivalenceKey: nameof(Sst2271VarStyleCodeFixProvider)),
                diagnostic);
        }
    }

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

        TypeSyntax? explicitType = null;
        if (typeSyntax.IsVar)
        {
            explicitType = TryGetExplicitType(model, typeSyntax);
            if (explicitType is null)
            {
                return null;
            }
        }

        return new NodeReplacement(typeSyntax, Rewrite(typeSyntax, explicitType));
    }

    /// <summary>Validates the exact emitted type name, retaining its parse for application.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="typeSyntax">The original inferred type node.</param>
    /// <returns>The bound explicit type syntax, or null when it cannot be named safely.</returns>
    private static TypeSyntax? TryGetExplicitType(SemanticModel model, TypeSyntax typeSyntax)
    {
        if (Sst2271VarStyleAnalyzer.ResolveVariableType(model, typeSyntax) is not { } resolvedType
            || !Sst2254ExplicitObjectCreationTypeAnalyzer.IsExpressibleTypeName(resolvedType))
        {
            return null;
        }

        var position = typeSyntax.SpanStart;
        var candidate = SyntaxFactory.ParseTypeName(resolvedType.ToMinimalDisplayString(model, position));
        var bound = model.GetSpeculativeTypeInfo(position, candidate, SpeculativeBindingOption.BindAsTypeOrNamespace).Type;
        return bound is not null && SymbolEqualityComparer.Default.Equals(bound, resolvedType) ? candidate : null;
    }

    /// <summary>Builds the requested spelling with the original type's outer trivia.</summary>
    /// <param name="typeSyntax">The type being replaced.</param>
    /// <param name="explicitType">The validated explicit type, or null to use var.</param>
    /// <returns>The replacement type.</returns>
    private static TypeSyntax Rewrite(TypeSyntax typeSyntax, TypeSyntax? explicitType) =>
        explicitType is not null
            ? explicitType.WithTriviaFrom(typeSyntax)
            : SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(typeSyntax.GetLeadingTrivia(), "var", typeSyntax.GetTrailingTrivia()));
}
