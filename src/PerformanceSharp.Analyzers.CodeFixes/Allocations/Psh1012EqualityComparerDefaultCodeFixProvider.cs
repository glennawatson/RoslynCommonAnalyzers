// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported boxing equality call to
/// <c>EqualityComparer&lt;T&gt;.Default.Equals(x, y)</c> (PSH1012). The comparer type is
/// spelled simple when the System.Collections.Generic import makes it resolve, and fully
/// qualified otherwise, so the fix never breaks the build.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1012EqualityComparerDefaultCodeFixProvider))]
[Shared]
public sealed class Psh1012EqualityComparerDefaultCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The simple name of the comparer type.</summary>
    private const string EqualityComparerTypeName = "EqualityComparer";

    /// <summary>The namespace that must be imported for the simple spelling.</summary>
    private const string GenericCollectionsNamespace = "System.Collections.Generic";

    /// <summary>The fully qualified spelling used when the simple name does not resolve.</summary>
    private const string QualifiedEqualityComparerExpression = "global::System.Collections.Generic.EqualityComparer";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.UseEqualityComparerDefault.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use EqualityComparer<T>.Default", nameof(Psh1012EqualityComparerDefaultCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported call and builds its comparer-based replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation
            || Psh1012EqualityComparerDefaultAnalyzer.TryGetBoxingComparison(model, invocation, CancellationToken.None) is not { } comparison)
        {
            return null;
        }

        var comparerSpelling = ResolvesEqualityComparer(model, invocation.SpanStart)
            ? EqualityComparerTypeName
            : QualifiedEqualityComparerExpression;
        var target = SyntaxFactory.ParseExpression($"{comparerSpelling}<{comparison.TypeParameter.Name}>.Default.Equals");

        var replacement = SyntaxFactory.InvocationExpression(
                target,
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(ImmutableArrays.Of(
                    SyntaxFactory.Argument(comparison.Left.WithoutTrivia()),
                    SyntaxFactory.Argument(comparison.Right.WithoutTrivia()).WithLeadingTrivia(SyntaxFactory.Space)))))
            .WithTriviaFrom(invocation);

        return new NodeReplacement(invocation, replacement);
    }

    /// <summary>Returns whether the comparer type resolves by simple name at a position.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The lookup position.</param>
    /// <returns><see langword="true"/> when the simple spelling binds.</returns>
    private static bool ResolvesEqualityComparer(SemanticModel model, int position)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(position, name: EqualityComparerTypeName))
        {
            if (candidate is INamedTypeSymbol { IsGenericType: true } named
                && named.ContainingNamespace.ToDisplayString() == GenericCollectionsNamespace)
            {
                return true;
            }
        }

        return false;
    }
}
