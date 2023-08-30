using System;
using System.Collections.Generic;
using System.Text;

namespace RoslynCommonAnalyzers;

internal static class UniqueLineCodeFixerHelper
{
    public static T? ConvertNodeIfAble<T, TParam>(this T node, Func<T, SeparatedSyntaxList<TParam>?> converterToList, Func<T, SeparatedSyntaxList<TParam>, T> addParameters)
        where T : SyntaxNode
        where TParam : SyntaxNode
    {
        var list = converterToList(node);

        if (list is null)
        {
            return null;
        }

        // Check if all arguments are on the same line as the method call
        if (list.Value.Count > 1 && list.Value.Any(p => p.GetLocation().GetLineSpan().StartLinePosition.Line != node.GetLocation().GetLineSpan().StartLinePosition.Line))
        {
            // Calculate the number of leading spaces of the method call
            var leadingSpaces = GetLeadingSpaces(node) + 4;

            // Create a new ArgumentListSyntax with each argument on its own line
            var newArguments = list.Value.Select(a => a.WithLeadingTrivia(SyntaxFactory.Whitespace(new string(' ', leadingSpaces)))).ToList();
            var newNode = addParameters(node, SyntaxFactory.SeparatedList(newArguments, Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed), newArguments.Count - 1)));
            ////var newNode = node.WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(newArguments, Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed), newArguments.Count - 1))));
            return newNode;
        }

        return null;
    }

    private static int GetLeadingSpaces(SyntaxNode? node)
    {
        return node is null ? 0 : node.GetLocation().GetLineSpan().StartLinePosition.Character;
    }
}
