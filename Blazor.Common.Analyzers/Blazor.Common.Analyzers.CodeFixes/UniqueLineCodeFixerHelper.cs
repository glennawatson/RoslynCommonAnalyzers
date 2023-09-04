// Copyright (c) 2023 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Blazor.Common.Analyzers;

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
        if (node is null)
        {
            return 0;
        }

        var tree = node.SyntaxTree;

        if (tree is null)
        {
            return 0;
        }

        var lineSpan = node.GetLocation().GetLineSpan();
        var startLine = tree.GetText().Lines[lineSpan.StartLinePosition.Line];

        var lineText = startLine.ToString();

        return lineText.TakeWhile(char.IsWhiteSpace).Count();
    }
}
