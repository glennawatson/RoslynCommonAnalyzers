// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a logging call so the exception reaches the dedicated exception argument: it inserts the exception
/// as a new first value argument and, when a value was standing in for it, removes that value and the
/// placeholder it filled. Shared by the SST2438 and SST2439 fixes.
/// </summary>
internal static class LoggerExceptionHoist
{
    /// <summary>Rewrites a call to carry the exception in its exception argument.</summary>
    /// <param name="invocation">The logging call.</param>
    /// <param name="exception">The expression to place in the exception argument.</param>
    /// <param name="insertIndex">The position the exception argument is inserted at.</param>
    /// <param name="removeIndex">The value argument to drop, or -1 to keep every value.</param>
    /// <param name="tailStart">The first value argument's position.</param>
    /// <returns>The rewritten call, or <see langword="null"/> when the shape no longer matches.</returns>
    public static InvocationExpressionSyntax? Rewrite(
        InvocationExpressionSyntax invocation,
        ExpressionSyntax exception,
        int insertIndex,
        int removeIndex,
        int tailStart)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (insertIndex < 0 || insertIndex >= arguments.Count)
        {
            return null;
        }

        var updated = new List<ArgumentSyntax>(arguments.Count + 1);
        for (var i = 0; i < arguments.Count; i++)
        {
            updated.Add(arguments[i].WithoutTrivia());
        }

        if (removeIndex >= tailStart && removeIndex < updated.Count)
        {
            updated[insertIndex] = TrimPlaceholder(updated[insertIndex], removeIndex - tailStart);
            updated.RemoveAt(removeIndex);
        }

        updated.Insert(insertIndex, SyntaxFactory.Argument(exception.WithoutTrivia()));
        return invocation.WithArgumentList(invocation.ArgumentList.WithArguments(SeparateWithSpaces(updated)));
    }

    /// <summary>Builds an argument list whose values are separated by a comma and a single space.</summary>
    /// <param name="arguments">The trivia-stripped arguments.</param>
    /// <returns>The separated list.</returns>
    private static SeparatedSyntaxList<ArgumentSyntax> SeparateWithSpaces(List<ArgumentSyntax> arguments)
    {
        if (arguments.Count <= 1)
        {
            return SyntaxFactory.SeparatedList(arguments);
        }

        var separators = new SyntaxToken[arguments.Count - 1];
        for (var i = 0; i < separators.Length; i++)
        {
            separators[i] = SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space);
        }

        return SyntaxFactory.SeparatedList(arguments, separators);
    }

    /// <summary>Removes one placeholder, and a padding space beside it, from a template argument when it exists.</summary>
    /// <param name="templateArgument">The template argument.</param>
    /// <param name="placeholderIndex">The placeholder position to remove.</param>
    /// <returns>The trimmed template argument, or the original when there is no such placeholder.</returns>
    private static ArgumentSyntax TrimPlaceholder(ArgumentSyntax templateArgument, int placeholderIndex)
    {
        if (templateArgument.Expression is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return templateArgument;
        }

        var text = literal.Token.ValueText;
        var placeholders = LogMessageTemplate.Parse(text);
        if (placeholderIndex < 0 || placeholderIndex >= placeholders.Length)
        {
            return templateArgument;
        }

        var newText = RemoveWithPadding(text, placeholders[placeholderIndex]);
        var newLiteral = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(newText))
            .WithTriviaFrom(literal);
        return templateArgument.WithExpression(newLiteral);
    }

    /// <summary>Removes a placeholder and one adjacent padding space from the template text.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="placeholder">The placeholder to remove.</param>
    /// <returns>The trimmed text.</returns>
    private static string RemoveWithPadding(string text, in LogPlaceholder placeholder)
    {
        var start = placeholder.ValueStart;
        var end = placeholder.ValueEnd;
        if (start > 0 && text[start - 1] == ' ')
        {
            start--;
        }
        else if (end < text.Length && text[end] == ' ')
        {
            end++;
        }

        return text.Remove(start, end - start);
    }
}
