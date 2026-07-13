// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a documentation comment that carries no text at all (SST1659) — one whose every line is a bare
/// <c>///</c>.
/// </summary>
/// <remarks>
/// <para>
/// An empty documentation comment is worse than a missing one. Every tool that asks "is this member
/// documented?" — this analyzer's own SST1600 family included — sees the <c>///</c> and answers yes, so the
/// member is recorded as done and never comes back.
/// </para>
/// <para>
/// A blank <c>///</c> line inside a documentation comment that says something is ordinary formatting, not a
/// mistake, so a comment is only reported when the <em>whole</em> comment is empty. Roslyn folds consecutive
/// <c>///</c> lines into a single trivia, which is exactly the unit this rule judges: the blank line and the
/// text around it arrive together, and the comment counts as empty only when no line in it has content.
/// </para>
/// <para>
/// Ordinary <c>//</c> and <c>/* */</c> comments are SST1120's business. This rule deliberately leaves them
/// alone so an empty comment is reported once, by one rule, rather than twice.
/// </para>
/// <para>
/// The rule is pure trivia: it registers a syntax-tree action, reads characters straight out of the source
/// text, and never touches the semantic model.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1659EmptyCommentAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The declarations a documentation comment can introduce.</summary>
    /// <remarks>
    /// A documentation comment attaches to a declaration and nothing else — one written anywhere else
    /// documents nothing, and the compiler says as much. Asking the driver for exactly these nodes means this
    /// rule reads only the trivia that could carry a documentation comment.
    /// </remarks>
    private static readonly SyntaxKind[] DocumentableKinds =
    [
        SyntaxKind.NamespaceDeclaration,
        SyntaxKind.FileScopedNamespaceDeclaration,
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.EnumDeclaration,
        SyntaxKind.DelegateDeclaration,
        SyntaxKind.EnumMemberDeclaration,
        SyntaxKind.FieldDeclaration,
        SyntaxKind.EventFieldDeclaration,
        SyntaxKind.EventDeclaration,
        SyntaxKind.PropertyDeclaration,
        SyntaxKind.IndexerDeclaration,
        SyntaxKind.MethodDeclaration,
        SyntaxKind.ConstructorDeclaration,
        SyntaxKind.DestructorDeclaration,
        SyntaxKind.OperatorDeclaration,
        SyntaxKind.ConversionOperatorDeclaration,
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DocumentationRules.EmptyComment);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // A tree action would have to walk every token in the file to find its trivia, and a profile put that
        // walk at most of this rule's cost — for a rule whose answer lives on a few dozen declarations. The
        // driver already visits those nodes for every other analyzer, so ask it for them instead of walking
        // the file again.
        context.RegisterSyntaxNodeAction(AnalyzeDeclaration, DocumentableKinds);
    }

    /// <summary>Gets the span to report for a documentation comment that has no text.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="trivia">The trivia to classify.</param>
    /// <param name="span">The span of the empty comment, delimiters included, when one is found.</param>
    /// <returns><see langword="true"/> when the trivia is a documentation comment with no text.</returns>
    /// <remarks>
    /// Only documentation comments are classified: an empty <c>//</c> or <c>/* */</c> belongs to SST1120, and
    /// reporting it here as well would put two squiggles on one comment.
    /// A documentation comment's <see cref="SyntaxTrivia.Span"/> starts <em>after</em> the leading <c>///</c>,
    /// which lives in the structure's exterior trivia, so its full span is the one that covers the whole
    /// comment. The result is trimmed back to the last character the comment actually wrote, which keeps the
    /// reported span — and the removal the code fix computes from it — off the following line.
    /// </remarks>
    internal static bool TryGetEmptyCommentSpan(SourceText text, SyntaxTrivia trivia, out TextSpan span)
    {
        switch (trivia.Kind())
        {
            case SyntaxKind.SingleLineDocumentationCommentTrivia:
            {
                return TryGetEmptyDocumentationSpan(text, trivia, allowStars: false, out span);
            }

            case SyntaxKind.MultiLineDocumentationCommentTrivia:
            {
                return TryGetEmptyDocumentationSpan(text, trivia, allowStars: true, out span);
            }

            default:
            {
                span = default;
                return false;
            }
        }
    }

    /// <summary>Reports the documentation comment on one declaration when it says nothing.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeDeclaration(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;
        if (!node.HasLeadingTrivia)
        {
            return;
        }

        var text = node.SyntaxTree.GetText(context.CancellationToken);
        foreach (var trivia in node.GetLeadingTrivia())
        {
            if (TryGetEmptyCommentSpan(text, trivia, out var span))
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(DocumentationRules.EmptyComment, node.SyntaxTree, span));
            }
        }
    }

    /// <summary>Gets the span to report for a documentation comment that says nothing.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="trivia">The documentation comment trivia.</param>
    /// <param name="allowStars">Whether <c>*</c> is part of the delimiters (a <c>/** */</c> comment).</param>
    /// <param name="span">The trimmed span of the empty comment when one is found.</param>
    /// <returns><see langword="true"/> when no line of the comment carries text.</returns>
    private static bool TryGetEmptyDocumentationSpan(SourceText text, SyntaxTrivia trivia, bool allowStars, out TextSpan span)
    {
        var full = trivia.FullSpan;
        span = default;
        for (var position = full.Start; position < full.End; position++)
        {
            var character = text[position];
            if (character is not '/' && !char.IsWhiteSpace(character) && !(allowStars && character is '*'))
            {
                return false;
            }
        }

        span = TrimTrailingWhitespace(text, full);
        return !span.IsEmpty;
    }

    /// <summary>Trims the trailing whitespace — including the terminating newline — off a span.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="span">The span to trim.</param>
    /// <returns>The span up to the last character the comment wrote.</returns>
    private static TextSpan TrimTrailingWhitespace(SourceText text, TextSpan span)
    {
        var end = span.End;
        while (end > span.Start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return TextSpan.FromBounds(span.Start, end);
    }
}
