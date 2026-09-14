// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the name-only matching of parameters and type parameters.</summary>
public class ParameterNamesTests
{
    /// <summary>The generic method the name tests read.</summary>
    private const string Method = "void M<TKey, TValue>(TKey key, TValue value) { }";

    /// <summary>Verifies a parameter and a type parameter are matched by their own list only.</summary>
    /// <param name="name">The name looked up.</param>
    /// <param name="expectedParameter">Whether a parameter has the name.</param>
    /// <param name="expectedTypeParameter">Whether a type parameter has the name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("key", true, false)]
    [Arguments("TValue", false, true)]
    [Arguments("missing", false, false)]
    public async Task EachListMatchesItsOwnNamesAsync(string name, bool expectedParameter, bool expectedTypeParameter)
    {
        var method = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(Method)!;

        await Assert.That(ParameterNames.Contains(method.ParameterList.Parameters, name)).IsEqualTo(expectedParameter);
        await Assert.That(ParameterNames.Contains(method.TypeParameterList!.Parameters, name)).IsEqualTo(expectedTypeParameter);
    }
}
