// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Appends <c>System.StringComparison.Ordinal</c> to a culture-sensitive string search or
/// comparison call (PSH1207). Ordinal is the cheapest comparison and the overwhelmingly
/// common intended semantics; the existing arguments and their separators are preserved.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1207SpecifyStringComparisonCodeFixProvider))]
[Shared]
public sealed class Psh1207SpecifyStringComparisonCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The fully-qualified ordinal comparison argument reused across fixes.</summary>
    private static readonly ExpressionSyntax OrdinalSyntax = SyntaxFactory.ParseExpression("System.StringComparison.Ordinal");

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.SpecifyStringComparison.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryGetInvocation(root, diagnostic, out var invocation))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Specify StringComparison.Ordinal",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, invocation!)),
                    equivalenceKey: nameof(Psh1207SpecifyStringComparisonCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetInvocation(editor.OriginalRoot, diagnostic, out var invocation))
        {
            return;
        }

        editor.ReplaceNode(invocation!.ArgumentList, AppendOrdinal(invocation.ArgumentList));
    }

    /// <summary>Appends the ordinal comparison argument to the reported invocation.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The reported invocation.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
        => document.WithSyntaxRoot(root.ReplaceNode(invocation.ArgumentList, AppendOrdinal(invocation.ArgumentList)));

    /// <summary>Resolves the diagnostic's reported method name to its enclosing invocation.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="invocation">The enclosing invocation when found.</param>
    /// <returns><see langword="true"/> when the invocation was found.</returns>
    private static bool TryGetInvocation(SyntaxNode root, Diagnostic diagnostic, out InvocationExpressionSyntax? invocation)
    {
        invocation = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<InvocationExpressionSyntax>();
        return invocation is not null;
    }

    /// <summary>Builds an argument list with <c>StringComparison.Ordinal</c> appended after the existing arguments.</summary>
    /// <param name="arguments">The original argument list.</param>
    /// <returns>The rewritten argument list.</returns>
    private static ArgumentListSyntax AppendOrdinal(ArgumentListSyntax arguments)
    {
        var separated = arguments.Arguments.GetWithSeparators();
        var items = new SyntaxNodeOrToken[separated.Count + 2];
        for (var i = 0; i < separated.Count; i++)
        {
            items[i] = separated[i];
        }

        items[separated.Count] = SyntaxFactory.Token(default, SyntaxKind.CommaToken, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        items[separated.Count + 1] = SyntaxFactory.Argument(OrdinalSyntax);
        return arguments.WithArguments(SyntaxFactory.SeparatedList<ArgumentSyntax>(items));
    }
}
