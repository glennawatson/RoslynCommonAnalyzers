// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Tests the name read from an identifier or a member access.</summary>
public class MemberReferenceNameTests
{
    /// <summary>Verifies only a plain identifier and a member access have a name, whatever the receiver.</summary>
    /// <param name="expression">The expression text.</param>
    /// <param name="expected">The expected name, or an empty string when the expression has none.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Value", "Value")]
    [Arguments("options.Value", "Value")]
    [Arguments("a.b.Value", "Value")]
    [Arguments("Load().Value", "Value")]
    [Arguments("this.Value", "Value")]
    [Arguments("Value<int>", "")]
    [Arguments("options?.Value", "")]
    [Arguments("Load()", "")]
    [Arguments("\"Value\"", "")]
    public async Task NameComesFromAnIdentifierOrMemberAccessAsync(string expression, string expected) =>
        await Assert.That(MemberReferenceName.Of(SyntaxFactory.ParseExpression(expression)) ?? string.Empty).IsEqualTo(expected);
}
