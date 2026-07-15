// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared rewrites that move parameter/argument list entries onto their own lines.</summary>
public sealed class UniqueLineCodeFixerHelperUnitTest
{
    /// <summary>Verifies a parameter list already spanning several lines is reflowed one parameter per line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SplitParametersOntoOwnLinesReflowsMultiLineParameterListAsync()
    {
        var method = ParseFirstMethod(
            """
            class C
            {
                void M(int a, int b,
                    int c)
                {
                }
            }
            """);

        var rewritten = UniqueLineCodeFixerHelper.SplitParametersOntoOwnLines(
            method,
            static n => n.ParameterList,
            static (n, list) => n.WithParameterList(list));

        await Assert.That(rewritten).IsNotEqualTo(method);
        await Assert.That(EndsEveryEntryLine(rewritten.ParameterList!.OpenParenToken, rewritten.ParameterList!.Parameters.GetSeparators())).IsTrue();
    }

    /// <summary>Verifies a single-line parameter list is left untouched.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SplitParametersOntoOwnLinesLeavesSingleLineListUnchangedAsync()
    {
        var method = ParseFirstMethod(
            """
            class C
            {
                void M(int a, int b)
                {
                }
            }
            """);

        var rewritten = UniqueLineCodeFixerHelper.SplitParametersOntoOwnLines(
            method,
            static n => n.ParameterList,
            static (n, list) => n.WithParameterList(list));

        await Assert.That(ReferenceEquals(rewritten, method)).IsTrue();
    }

    /// <summary>Verifies an argument list already spanning several lines is reflowed one argument per line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SplitArgumentsOntoOwnLinesReflowsMultiLineArgumentListAsync()
    {
        var invocation = ParseFirstInvocation(
            """
            class C
            {
                void M()
                {
                    N(1, 2,
                        3);
                }
            }
            """);

        var rewritten = UniqueLineCodeFixerHelper.SplitArgumentsOntoOwnLines(
            invocation,
            static n => n.ArgumentList,
            static (n, list) => n.WithArgumentList(list));

        await Assert.That(rewritten).IsNotEqualTo(invocation);
        await Assert.That(EndsEveryEntryLine(rewritten.ArgumentList.OpenParenToken, rewritten.ArgumentList.Arguments.GetSeparators())).IsTrue();
    }

    /// <summary>Verifies a multi-line type parameter list is reflowed one entry per line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SplitAngleBracketedListOntoOwnLinesReflowsMultiLineTypeParameterListAsync()
    {
        var typeParameterList = ParseFirstTypeParameterList(
            """
            class C<T1, T2,
                T3>
            {
            }
            """);

        var rewritten = UniqueLineCodeFixerHelper.SplitAngleBracketedListOntoOwnLines(
            typeParameterList,
            typeParameterList.Parameters,
            (list, endOfLine) => SyntaxFactory.TypeParameterList(list)
                .WithLessThanToken(typeParameterList.LessThanToken.WithTrailingTrivia(endOfLine))
                .WithGreaterThanToken(typeParameterList.GreaterThanToken));

        await Assert.That(rewritten).IsNotEqualTo(typeParameterList);
        await Assert.That(EndsEveryEntryLine(rewritten.LessThanToken, rewritten.Parameters.GetSeparators())).IsTrue();
    }

    /// <summary>Verifies a single-line type parameter list is left untouched.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SplitAngleBracketedListOntoOwnLinesLeavesSingleLineListUnchangedAsync()
    {
        var typeParameterList = ParseFirstTypeParameterList(
            """
            class C<T1, T2>
            {
            }
            """);

        var rewritten = UniqueLineCodeFixerHelper.SplitAngleBracketedListOntoOwnLines(
            typeParameterList,
            typeParameterList.Parameters,
            (list, endOfLine) => SyntaxFactory.TypeParameterList(list)
                .WithLessThanToken(typeParameterList.LessThanToken.WithTrailingTrivia(endOfLine))
                .WithGreaterThanToken(typeParameterList.GreaterThanToken));

        await Assert.That(ReferenceEquals(rewritten, typeParameterList)).IsTrue();
    }

    /// <summary>Returns whether the opening token and every separator carry an end-of-line, one entry per line.</summary>
    /// <param name="opener">The opening token (paren or angle bracket).</param>
    /// <param name="separators">The list's comma separators.</param>
    /// <returns><see langword="true"/> when every entry begins on its own line.</returns>
    private static bool EndsEveryEntryLine(SyntaxToken opener, IEnumerable<SyntaxToken> separators)
    {
        if (!opener.TrailingTrivia.Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia)))
        {
            return false;
        }

        foreach (var separator in separators)
        {
            if (!separator.TrailingTrivia.Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Parses the first method declaration from a single-type snippet.</summary>
    /// <param name="source">The source snippet.</param>
    /// <returns>The first method declaration.</returns>
    private static MethodDeclarationSyntax ParseFirstMethod(string source)
        => (MethodDeclarationSyntax)((TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0]).Members[0];

    /// <summary>Parses the first invocation expression from a single-type snippet.</summary>
    /// <param name="source">The source snippet.</param>
    /// <returns>The first invocation expression.</returns>
    private static InvocationExpressionSyntax ParseFirstInvocation(string source)
    {
        var method = ParseFirstMethod(source);
        return (InvocationExpressionSyntax)((ExpressionStatementSyntax)method.Body!.Statements[0]).Expression;
    }

    /// <summary>Parses the first type parameter list from a single-type snippet.</summary>
    /// <param name="source">The source snippet.</param>
    /// <returns>The first type parameter list.</returns>
    private static TypeParameterListSyntax ParseFirstTypeParameterList(string source)
        => ((TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0]).TypeParameterList!;
}
