// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>
/// Answers whether a preprocessor directive lies across the region a fix is about to rearrange.
/// </summary>
/// <remarks>
/// A directive belongs to a position in the file, not to the node it sits above. A fix that moves,
/// merges or drops a node carries whichever half of a directive pair that node's trivia happens to
/// hold and leaves the other half where it was: an <c>#endregion</c> can end up above its own
/// <c>#region</c> (CS1028), a member can leave the <c>#if</c> that decided whether it compiled, and a
/// member can cross a <c>#pragma warning disable</c> and start — or stop — warning. None of that is
/// visible in the node being rearranged, so a fix that reorders siblings asks here first and declines.
/// </remarks>
internal static class DirectiveBoundaries
{
    /// <summary>Returns whether a directive starts inside a span.</summary>
    /// <param name="container">A node enclosing the span.</param>
    /// <param name="span">The region the fix rearranges.</param>
    /// <returns><see langword="true"/> when a directive lies in the span.</returns>
    public static bool Cross(SyntaxNode container, TextSpan span)
    {
        if (!container.ContainsDirectives)
        {
            return false;
        }

        for (var directive = container.GetFirstDirective(); directive is not null; directive = directive.GetNextDirective())
        {
            if (directive.SpanStart >= span.End)
            {
                return false;
            }

            if (span.Contains(directive.SpanStart))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a directive lies between a type's or enum's braces.</summary>
    /// <param name="declaration">The declaration whose members a fix reorders.</param>
    /// <returns><see langword="true"/> when a directive sits among the members.</returns>
    /// <remarks>
    /// The span is the braces rather than the whole declaration: a file-scoped <c>#nullable</c> above
    /// the declaration is part of its leading trivia, and so of its full span, but no member crosses it.
    /// </remarks>
    public static bool SeparateMembers(BaseTypeDeclarationSyntax declaration)
        => !declaration.OpenBraceToken.IsKind(SyntaxKind.None)
            && Cross(declaration, TextSpan.FromBounds(declaration.OpenBraceToken.SpanStart, declaration.CloseBraceToken.Span.End));

    /// <summary>Returns whether a directive lies between two nodes a fix brings together.</summary>
    /// <param name="first">One of the nodes.</param>
    /// <param name="second">The other node.</param>
    /// <returns><see langword="true"/> when a directive sits in the gap between them.</returns>
    /// <remarks>
    /// Only the gap is weighed, so a directive above both — or below both — does not block a fix that
    /// never moves anything across it.
    /// </remarks>
    public static bool Separate(SyntaxNode first, SyntaxNode second)
    {
        if (first.Parent is not { } container)
        {
            return false;
        }

        if (first.Span.End <= second.Span.Start)
        {
            return Cross(container, TextSpan.FromBounds(first.Span.End, second.Span.Start));
        }

        // One node inside the other is not a pair a fix reorders, so there is no gap to weigh.
        return second.Span.End <= first.Span.Start
            && Cross(container, TextSpan.FromBounds(second.Span.End, first.Span.Start));
    }

    /// <summary>Returns whether a conditional directive appears anywhere in a tree.</summary>
    /// <param name="root">The syntax root.</param>
    /// <returns><see langword="true"/> when an <c>#if</c> family directive is present.</returns>
    /// <remarks>
    /// A conditional opened before a declaration and closed after it puts every member inside a region
    /// that only some compilations see, which no span inside the declaration reveals.
    /// </remarks>
    public static bool AnyConditional(SyntaxNode root)
    {
        if (!root.ContainsDirectives)
        {
            return false;
        }

        for (var directive = root.GetFirstDirective(); directive is not null; directive = directive.GetNextDirective())
        {
            if (directive.Kind() is SyntaxKind.IfDirectiveTrivia
                or SyntaxKind.ElifDirectiveTrivia
                or SyntaxKind.ElseDirectiveTrivia
                or SyntaxKind.EndIfDirectiveTrivia)
            {
                return true;
            }
        }

        return false;
    }
}
