// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared rewrites that move parameter/argument list entries onto their own lines.</summary>
public sealed class UniqueLineCodeFixerHelperUnitTest
{
    /// <summary>Verifies line endings use the first newline and default to LF when no newline exists.</summary>
    /// <param name="source">The source whose line endings are inspected.</param>
    /// <param name="expected">The expected newline sequence.</param>
    /// <param name="elastic">Whether formatter-adjustable trivia is requested.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C {}", "\n", false)]
    [Arguments("class C {}", "\n", true)]
    [Arguments("\nclass C {}", "\n", false)]
    [Arguments("\nclass C {}", "\n", true)]
    [Arguments("\r\nclass C {}", "\r\n", false)]
    [Arguments("\r\nclass C {}", "\r\n", true)]
    [Arguments("class C\n{\r\n}", "\n", false)]
    [Arguments("class C\r\n{\n}", "\r\n", true)]
    public async Task EndOfLineMatchesSourceAsync(string source, string expected, bool elastic)
    {
        var node = SyntaxFactory.ParseCompilationUnit(source);
        var trivia = UniqueLineCodeFixerHelperExtensions.GetEndOfLine(node, elastic);
        await Assert.That(trivia.ToFullString()).IsEqualTo(expected);
        await Assert.That(trivia.IsKind(SyntaxKind.EndOfLineTrivia)).IsTrue();
        await Assert.That(trivia.HasAnnotation(SyntaxAnnotation.ElasticAnnotation)).IsEqualTo(elastic);
    }

    /// <summary>Verifies a list extractor returning no list preserves its owner.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingListsLeaveOwnerUnchangedAsync()
    {
        var method = ParseFirstMethod("class C { void M() {} }");
        var invocation = ParseFirstInvocation("class C { void M() { N(); } }");
        var parameters = UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(method, static _ => null, static (node, list) => node.WithParameterList(list));
        var arguments = UniqueLineCodeFixerHelperExtensions.SplitArgumentsOntoOwnLines(invocation, static _ => null, static (node, list) => node.WithArgumentList(list));
        await Assert.That(parameters).IsSameReferenceAs(method);
        await Assert.That(arguments).IsSameReferenceAs(invocation);
    }

    /// <summary>Verifies empty and single-entry lists never invoke a rewrite.</summary>
    /// <param name="parameters">The parameter list content.</param>
    /// <param name="arguments">The argument list content.</param>
    /// <param name="typeParameters">The type parameter list content.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "", "")]
    [Arguments("\nint value", "\n1", "\nT")]
    public async Task ShortListsLeaveOwnerUnchangedAsync(string parameters, string arguments, string typeParameters)
    {
        var method = ParseFirstMethod($"class C {{ void M({parameters}) {{}} }}");
        var invocation = ParseFirstInvocation($"class C {{ void M() {{ N({arguments}); }} }}");
        var typeList = SyntaxFactory.TypeParameterList();
        if (typeParameters.Length > 0)
        {
            typeList = ParseFirstTypeParameterList($"class C<{typeParameters}> {{}}");
        }

        var rewrittenMethod = UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(method, static node => node.ParameterList, static (node, list) => node.WithParameterList(list));
        var rewrittenInvocation = UniqueLineCodeFixerHelperExtensions.SplitArgumentsOntoOwnLines(invocation, static node => node.ArgumentList, static (node, list) => node.WithArgumentList(list));
        var rewrittenTypes = UniqueLineCodeFixerHelperExtensions.SplitAngleBracketedListOntoOwnLines(typeList, typeList.Parameters, static (list, _) => SyntaxFactory.TypeParameterList(list));
        await Assert.That(rewrittenMethod).IsSameReferenceAs(method);
        await Assert.That(rewrittenInvocation).IsSameReferenceAs(invocation);
        await Assert.That(rewrittenTypes).IsSameReferenceAs(typeList);
    }

    /// <summary>Verifies reflow preserves source newlines and indents parameters relative to their declaration.</summary>
    /// <param name="newline">The source newline sequence.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("\n")]
    [Arguments("\r\n")]
    public async Task ParameterReflowPreservesIndentationAndNewlinesAsync(string newline)
    {
        var method = ParseFirstMethod($"class C{newline}{{{newline}  void M(int first,{newline}int second) {{}}{newline}}}");
        var rewritten = UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(method, static node => node.ParameterList, static (node, list) => node.WithParameterList(list));
        await Assert.That(rewritten.ToFullString()).IsEqualTo($"  void M({newline}      int first,{newline}      int second){{}}{newline}");
    }

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

        var rewritten = UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(
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

        var rewritten = UniqueLineCodeFixerHelperExtensions.SplitParametersOntoOwnLines(
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

        var rewritten = UniqueLineCodeFixerHelperExtensions.SplitArgumentsOntoOwnLines(
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

        var rewritten = UniqueLineCodeFixerHelperExtensions.SplitAngleBracketedListOntoOwnLines(
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

        var rewritten = UniqueLineCodeFixerHelperExtensions.SplitAngleBracketedListOntoOwnLines(
            typeParameterList,
            typeParameterList.Parameters,
            (list, endOfLine) => SyntaxFactory.TypeParameterList(list)
                .WithLessThanToken(typeParameterList.LessThanToken.WithTrailingTrivia(endOfLine))
                .WithGreaterThanToken(typeParameterList.GreaterThanToken));

        await Assert.That(ReferenceEquals(rewritten, typeParameterList)).IsTrue();
    }

    /// <summary>Parses the first method declaration from a single-type snippet.</summary>
    /// <param name="source">The source snippet.</param>
    /// <returns>The first method declaration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodDeclarationSyntax ParseFirstMethod(string source) =>
        (MethodDeclarationSyntax)((TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0]).Members[0];

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TypeParameterListSyntax ParseFirstTypeParameterList(string source) =>
        ((TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0]).TypeParameterList!;

    /// <summary>Returns whether the opening token and every separator carry an end-of-line, one entry per line.</summary>
    /// <param name="opener">The opening token (paren or angle bracket).</param>
    /// <param name="separators">The list's comma separators.</param>
    /// <returns><see langword="true"/> when every entry begins on its own line.</returns>
    private static bool EndsEveryEntryLine(SyntaxToken opener, IEnumerable<SyntaxToken> separators)
    {
        if (!opener.TrailingTrivia.Any(static t => t.IsKind(SyntaxKind.EndOfLineTrivia)))
        {
            return false;
        }

        foreach (var separator in separators)
        {
            if (!separator.TrailingTrivia.Any(static t => t.IsKind(SyntaxKind.EndOfLineTrivia)))
            {
                return false;
            }
        }

        return true;
    }
}
