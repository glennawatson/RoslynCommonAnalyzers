// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared reading of a call's written argument shape.</summary>
public sealed class ArgumentListFactsUnitTest
{
    /// <summary>Verifies plain positional arguments, and no arguments at all, are positional.</summary>
    /// <param name="call">The call as written.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("M()")]
    [Arguments("M(a, b + 1)")]
    public async Task PositionalArgumentsAreNotReportedAsync(string call) =>
        await Assert.That(ArgumentListFacts.HasNonPositionalArgument(Parse(call))).IsFalse();

    /// <summary>Verifies a named argument or a by-reference argument is reported.</summary>
    /// <param name="call">The call as written.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("M(a, value: b)")]
    [Arguments("M(ref a)")]
    [Arguments("M(out var a)")]
    [Arguments("M(in a)")]
    public async Task NamedOrByReferenceArgumentsAreReportedAsync(string call) =>
        await Assert.That(ArgumentListFacts.HasNonPositionalArgument(Parse(call))).IsTrue();

    /// <summary>Parses a call expression.</summary>
    /// <param name="call">The call as written.</param>
    /// <returns>The parsed invocation.</returns>
    private static InvocationExpressionSyntax Parse(string call) => (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(call);
}
