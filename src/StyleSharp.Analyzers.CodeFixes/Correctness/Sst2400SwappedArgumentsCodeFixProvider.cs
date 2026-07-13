// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Puts two transposed arguments back in the parameters' order (SST2400).
/// </summary>
/// <remarks>
/// Only the two reported positions move, and only their expressions do: each argument keeps the trivia that
/// was around it, so a call spread over several lines keeps its shape and only the two names change places.
/// The analyzer has already established that the two parameters share a type and a ref kind, so the
/// reordered call binds to the same method it did before.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2400SwappedArgumentsCodeFixProvider))]
[Shared]
public sealed class Sst2400SwappedArgumentsCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.SwappedArguments.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Swap the arguments into the parameter order",
            nameof(Sst2400SwappedArgumentsCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Applies one SST2400 swap for the reported argument.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original when the reported shape no longer matches.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
        => TryRewrite(root, diagnostic) is { } edit
            ? document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))
            : document;

    /// <summary>Resolves the reported argument and swaps it with the position it belongs in.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (!TryGetPartner(diagnostic, out var partner)
            || root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<ArgumentSyntax>() is not { } argument
            || argument.Parent is not ArgumentListSyntax list)
        {
            return null;
        }

        var index = list.Arguments.IndexOf(argument);
        if (!IsSwappablePair(list, index, partner))
        {
            return null;
        }

        return new NodeReplacement(list, Swap(list, index, partner), current => Rewrite(current, index, partner));
    }

    /// <summary>Re-applies the swap to the argument list as it stands after any nested batch edit.</summary>
    /// <param name="current">The current argument list.</param>
    /// <param name="index">The reported argument's position.</param>
    /// <param name="partner">The position it belongs in.</param>
    /// <returns>The reordered list, or the node unchanged when it no longer matches.</returns>
    private static SyntaxNode Rewrite(SyntaxNode current, int index, int partner)
        => current is ArgumentListSyntax list && IsSwappablePair(list, index, partner)
            ? Swap(list, index, partner)
            : current;

    /// <summary>Exchanges the expressions at two positions, leaving each argument's trivia where it was.</summary>
    /// <param name="list">The argument list.</param>
    /// <param name="index">The reported argument's position.</param>
    /// <param name="partner">The position it belongs in.</param>
    /// <returns>The reordered argument list.</returns>
    private static ArgumentListSyntax Swap(ArgumentListSyntax list, int index, int partner)
    {
        var arguments = list.Arguments;
        var first = arguments[index];
        var second = arguments[partner];
        var swappedFirst = first.WithExpression(second.Expression.WithTriviaFrom(first.Expression));
        var swappedSecond = second.WithExpression(first.Expression.WithTriviaFrom(second.Expression));

        // Replace by position, re-reading the second argument from the list the first replacement produced:
        // the nodes of the original list do not belong to it any more.
        var swapped = arguments.Replace(arguments[index], swappedFirst);
        swapped = swapped.Replace(swapped[partner], swappedSecond);
        return list.WithArguments(swapped);
    }

    /// <summary>Returns whether both reported positions still exist in the list.</summary>
    /// <param name="list">The argument list.</param>
    /// <param name="index">The reported argument's position.</param>
    /// <param name="partner">The position it belongs in.</param>
    /// <returns><see langword="true"/> when the swap can be applied.</returns>
    private static bool IsSwappablePair(ArgumentListSyntax list, int index, int partner)
        => index >= 0
            && partner >= 0
            && index != partner
            && index < list.Arguments.Count
            && partner < list.Arguments.Count;

    /// <summary>Reads the position the reported argument belongs in.</summary>
    /// <param name="diagnostic">The diagnostic to read.</param>
    /// <param name="partner">The transposed partner's position.</param>
    /// <returns><see langword="true"/> when the diagnostic carries a usable position.</returns>
    private static bool TryGetPartner(Diagnostic diagnostic, out int partner)
    {
        partner = -1;
        return diagnostic.Properties.TryGetValue(Sst2400SwappedArgumentsAnalyzer.SwapWithKey, out var value)
            && value is not null
            && int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out partner);
    }
}
