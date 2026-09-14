// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Narrows a reported interface declaration to the concrete type it only ever holds (PSH1415):
/// <c>IList&lt;int&gt; items = new List&lt;int&gt;();</c> becomes
/// <c>List&lt;int&gt; items = new List&lt;int&gt;();</c>. The replacement type is written in its
/// minimal form for the declaration's position and speculatively bound before the fix is offered,
/// so a name that would not resolve there is never produced.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1415UseConcreteTypeCodeFixProvider))]
[Shared]
public sealed class Psh1415UseConcreteTypeCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.UseConcreteType.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            TryCreateTitle,
            static _ => nameof(Psh1415UseConcreteTypeCodeFixProvider),
            TryRewrite);

    /// <summary>Resolves the reported declaration and builds its concrete type syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is TypeSyntax declaredType
            && TryGetReplacement(model, declaredType, out var replacement)
            ? new NodeReplacement(declaredType, PrepareReplacement(declaredType, replacement!))
            : null;

    /// <summary>Builds the concrete type syntax for a reported declaration, and proves it binds.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="declaredType">The declared type syntax to rewrite.</param>
    /// <param name="replacement">The replacement type syntax when one could be built and bound.</param>
    /// <returns><see langword="true"/> when a replacement was built.</returns>
    private static bool TryGetReplacement(SemanticModel model, TypeSyntax declaredType, out TypeSyntax? replacement)
    {
        replacement = null;
        if (declaredType.Parent is not VariableDeclarationSyntax { Variables.Count: 1 } declaration
            || declaration.Variables[0].Initializer?.Value is not ObjectCreationExpressionSyntax creation
            || model.GetTypeInfo(creation).Type is not INamedTypeSymbol { TypeKind: not TypeKind.Error } concrete)
        {
            return false;
        }

        var position = declaredType.SpanStart;
        var candidate = SyntaxFactory.ParseTypeName(concrete.ToMinimalDisplayString(model, position));
        if (!BindsToConcreteType(model, position, candidate, concrete))
        {
            return false;
        }

        replacement = candidate;
        return true;
    }

    /// <summary>Words the action when the reported declared type has a concrete replacement that binds.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The code action title, or <see langword="null"/> when no concrete type applies.</returns>
    private static string? TryCreateTitle(SyntaxNode root, SemanticModel model, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is TypeSyntax declaredType && TryGetReplacement(model, declaredType, out _)
            ? "Declare the concrete type"
            : null;

    /// <summary>Speculatively binds the replacement type name and confirms it resolves to the concrete type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The declaration's position, used as the speculative binding context.</param>
    /// <param name="candidate">The replacement type syntax.</param>
    /// <param name="concrete">The concrete type it must resolve to.</param>
    /// <returns><see langword="true"/> when the replacement names exactly that type.</returns>
    private static bool BindsToConcreteType(SemanticModel model, int position, TypeSyntax candidate, INamedTypeSymbol concrete) =>
        model.GetSpeculativeTypeInfo(position, candidate, SpeculativeBindingOption.BindAsTypeOrNamespace).Type is { } bound
            && SymbolEqualityComparer.Default.Equals(bound, concrete);

    /// <summary>Adds output trivia and annotations only when applying the validated type.</summary>
    /// <param name="declaredType">The original declaration's type.</param>
    /// <param name="candidate">The concrete type name that passed speculative binding.</param>
    /// <returns>The replacement type ready to insert.</returns>
    private static TypeSyntax PrepareReplacement(TypeSyntax declaredType, TypeSyntax candidate)
    {
        var replacement = candidate.WithTriviaFrom(declaredType);
        return replacement.WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation);
    }
}
