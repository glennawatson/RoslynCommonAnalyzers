// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests exception insertion and removal of matching template placeholders.</summary>
public class LoggerExceptionHoistTests
{
    /// <summary>Verifies placeholder padding, missing placeholders, and nonliteral templates preserve the remaining call.</summary>
    /// <param name="source">The original logging invocation.</param>
    /// <param name="removeIndex">The argument to remove.</param>
    /// <param name="tailStart">The first template value argument.</param>
    /// <param name="expected">The resulting invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Log(\"{Error} failed\", error)", 1, 1, "Log(error, \"failed\")")]
    [Arguments("Log(\"Failed {Error}\", error)", 1, 1, "Log(error, \"Failed\")")]
    [Arguments("Log(\"{Error}\", error)", 1, 1, "Log(error, \"\")")]
    [Arguments("Log(\"{Error}!\", error)", 1, 1, "Log(error, \"!\")")]
    [Arguments("Log(\"Failed:{Error}\", error)", 1, 1, "Log(error, \"Failed:\")")]
    [Arguments("Log(template, error)", 1, 1, "Log(error, template)")]
    [Arguments("Log(42, error)", 1, 1, "Log(error, 42)")]
    [Arguments("Log(\"Failed\", error)", 1, 1, "Log(error, \"Failed\")")]
    [Arguments("Log(\"{First}\", value, error)", 2, 1, "Log(error, \"{First}\", value)")]
    [Arguments("Log(\"Failed\", value)", -1, 1, "Log(error, \"Failed\", value)")]
    [Arguments("Log(\"Failed\", value)", 9, 1, "Log(error, \"Failed\", value)")]
    [Arguments("Log(\"{Error}\")", 0, 0, "Log(error)")]
    public async Task RewritePreservesUnremovedArgumentsAsync(string source, int removeIndex, int tailStart, string expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(source);
        var rewritten = LoggerExceptionHoist.Rewrite(invocation, SyntaxFactory.IdentifierName("error"), 0, removeIndex, tailStart);
        await Assert.That(rewritten?.NormalizeWhitespace().ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies insertion outside the argument list is rejected.</summary>
    /// <param name="insertIndex">The invalid insertion position.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    public async Task InvalidInsertionIsRejectedAsync(int insertIndex)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression("Log(template)");
        await Assert.That(LoggerExceptionHoist.Rewrite(invocation, SyntaxFactory.IdentifierName("error"), insertIndex, -1, 1)).IsNull();
    }
}
