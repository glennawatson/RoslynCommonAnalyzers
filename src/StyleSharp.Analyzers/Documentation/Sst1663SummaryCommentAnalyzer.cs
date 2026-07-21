// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a single <c>//</c> comment sitting immediately above a public member, with no blank line between
/// them, that reads like a summary (SST1663). Such a comment is describing the member — which is what a
/// <c>/// &lt;summary&gt;</c> documentation comment is for. Off by default because not every adjacent comment
/// is a summary.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1663SummaryCommentAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DocumentationRules.SummaryComment);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.EnumDeclaration,
            SyntaxKind.DelegateDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.IndexerDeclaration,
            SyntaxKind.EventDeclaration,
            SyntaxKind.EventFieldDeclaration,
            SyntaxKind.FieldDeclaration,
            SyntaxKind.OperatorDeclaration,
            SyntaxKind.ConversionOperatorDeclaration);
    }

    /// <summary>Reports a summary-like <c>//</c> comment directly above a public member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var member = (MemberDeclarationSyntax)context.Node;
        if (!ModifierListHelper.Contains(member.Modifiers, SyntaxKind.PublicKeyword))
        {
            return;
        }

        if (XmlDocumentationHelper.GetDocumentationComment(member) is not null)
        {
            // Already documented — leave the existing documentation alone.
            return;
        }

        if (FindSummaryLikeComment(member) is not { } comment)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.SummaryComment, comment.GetLocation()));
    }

    /// <summary>Finds a lone, prose-like <c>//</c> comment on the line immediately above the member.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The qualifying comment, or <see langword="null"/>.</returns>
    private static SyntaxTrivia? FindSummaryLikeComment(MemberDeclarationSyntax member)
    {
        var leading = member.GetLeadingTrivia();
        var commentIndex = LastSingleLineCommentIndex(leading);
        if (commentIndex < 0)
        {
            return null;
        }

        var comment = leading[commentIndex];
        if (!IsPlainDoubleSlashProse(comment.ToString()))
        {
            return null;
        }

        if (!ImmediatelyPrecedesMember(leading, commentIndex)
            || !StartsItsOwnLine(leading, commentIndex)
            || HasContiguousCommentAbove(leading, commentIndex))
        {
            return null;
        }

        return comment;
    }

    /// <summary>Returns the index of the last single-line comment in a trivia list, or <c>-1</c>.</summary>
    /// <param name="leading">The leading trivia.</param>
    /// <returns>The index, or <c>-1</c>.</returns>
    private static int LastSingleLineCommentIndex(SyntaxTriviaList leading)
    {
        var found = -1;
        for (var i = 0; i < leading.Count; i++)
        {
            if (leading[i].IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                found = i;
            }
        }

        return found;
    }

    /// <summary>Returns whether only whitespace and exactly one line break separate the comment from the member.</summary>
    /// <param name="leading">The leading trivia.</param>
    /// <param name="commentIndex">The comment's index.</param>
    /// <returns><see langword="true"/> when there is no blank line between the comment and the member.</returns>
    private static bool ImmediatelyPrecedesMember(SyntaxTriviaList leading, int commentIndex)
    {
        var lineBreaks = 0;
        for (var i = commentIndex + 1; i < leading.Count; i++)
        {
            var trivia = leading[i];
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                lineBreaks++;
            }
            else if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                return false;
            }
        }

        return lineBreaks == 1;
    }

    /// <summary>Returns whether the comment begins its own line (nothing but a line break precedes it).</summary>
    /// <param name="leading">The leading trivia.</param>
    /// <param name="commentIndex">The comment's index.</param>
    /// <returns><see langword="true"/> when the comment owns its line.</returns>
    private static bool StartsItsOwnLine(SyntaxTriviaList leading, int commentIndex)
    {
        for (var i = commentIndex - 1; i >= 0; i--)
        {
            var trivia = leading[i];
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                continue;
            }

            return trivia.IsKind(SyntaxKind.EndOfLineTrivia);
        }

        return true;
    }

    /// <summary>Returns whether another <c>//</c> comment sits contiguously on the line above this one.</summary>
    /// <param name="leading">The leading trivia.</param>
    /// <param name="commentIndex">The comment's index.</param>
    /// <returns><see langword="true"/> when this comment is part of a multi-line <c>//</c> block.</returns>
    private static bool HasContiguousCommentAbove(SyntaxTriviaList leading, int commentIndex)
    {
        var lineBreaks = 0;
        for (var i = commentIndex - 1; i >= 0; i--)
        {
            var trivia = leading[i];
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                continue;
            }

            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                lineBreaks++;
                if (lineBreaks > 1)
                {
                    return false;
                }

                continue;
            }

            return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia);
        }

        return false;
    }

    /// <summary>Returns whether a comment is exactly a <c>//</c> comment whose text reads like prose.</summary>
    /// <param name="text">The comment text.</param>
    /// <returns><see langword="true"/> for a plain <c>//</c> comment starting with a letter.</returns>
    private static bool IsPlainDoubleSlashProse(string text)
    {
        const string Prefix = "//";

        // Reject '///' (documentation) and '////' or more (commented-out code keeps its extra slashes as text).
        if (!text.StartsWith(Prefix, StringComparison.Ordinal)
            || (text.Length > Prefix.Length && text[Prefix.Length] == '/'))
        {
            return false;
        }

        var i = Prefix.Length;
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        // A summary reads like a sentence; requiring a leading letter drops separators ('// ----') and code.
        return i < text.Length && char.IsLetter(text[i]);
    }
}
