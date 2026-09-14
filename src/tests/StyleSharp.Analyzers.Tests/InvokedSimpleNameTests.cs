// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the reading of the simple name an invocation calls.</summary>
public class InvokedSimpleNameTests
{
    /// <summary>Verifies each callee shape yields its name and points at it, and other shapes yield nothing.</summary>
    /// <param name="source">The invocation expression.</param>
    /// <param name="expectedName">The invoked name, or <see langword="null"/> when there is none.</param>
    /// <param name="expectedLocationText">The source text the location covers.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Assert.Equal(1, value)", "Equal", "Equal")]
    [Arguments("Assert.Equal<int>(1, value)", "Equal", "Equal<int>")]
    [Arguments("Equal(1, value)", "Equal", "Equal")]
    [Arguments("Equal<int>(1, value)", "Equal", "Equal<int>")]
    [Arguments("factory()(value)", null, "factory()(value)")]
    public async Task ReadsTheCalledNameAsync(string source, string? expectedName, string expectedLocationText)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(source);

        await Assert.That(InvokedSimpleName.Of(invocation)).IsEqualTo(expectedName);
        await Assert.That(source.Substring(InvokedSimpleName.LocationOf(invocation).SourceSpan.Start, InvokedSimpleName.LocationOf(invocation).SourceSpan.Length)).IsEqualTo(expectedLocationText);
    }

    /// <summary>Verifies a conditional call reads the member it binds.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConditionalCallReadsTheBoundMemberAsync()
    {
        var access = (ConditionalAccessExpressionSyntax)SyntaxFactory.ParseExpression("stream?.ReadAsync(buffer)");
        var invocation = (InvocationExpressionSyntax)access.WhenNotNull;

        await Assert.That(InvokedSimpleName.Of(invocation)).IsEqualTo("ReadAsync");
    }
}
