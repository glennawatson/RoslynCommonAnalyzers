// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests reading the name a body-scope declaration introduces.</summary>
public class DeclarationIdentifierTests
{
    /// <summary>A method declaring one name of every kind the helper reads.</summary>
    private const string Source =
        "class C { void M(int parameter) { var variable = 0; foreach (var item in new int[0]) { } "
        + "try { } catch (System.Exception error) { } if (parameter is int designation) { } void Local() { } } }";

    /// <summary>Verifies each kind of declaration yields its name.</summary>
    /// <param name="expected">The declared name the test looks for.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("parameter")]
    [Arguments("variable")]
    [Arguments("item")]
    [Arguments("error")]
    [Arguments("designation")]
    [Arguments("Local")]
    public async Task ReadsTheDeclaredNameAsync(string expected)
    {
        var root = SyntaxFactory.ParseCompilationUnit(Source);

        var names = root.DescendantNodes().Select(DeclarationIdentifier.Of).Where(static token => token.RawKind != 0).Select(static token => token.ValueText).ToList();

        await Assert.That(names).Contains(expected);
    }

    /// <summary>Verifies a node that declares nothing yields the default token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OtherNodesYieldNoIdentifierAsync()
    {
        var expression = SyntaxFactory.ParseExpression("a + b");

        await Assert.That(DeclarationIdentifier.Of(expression).IsKind(SyntaxKind.None)).IsTrue();
    }
}
