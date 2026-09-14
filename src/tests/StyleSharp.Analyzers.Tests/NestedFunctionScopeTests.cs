// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests whether a lambda, anonymous method or local function encloses a node below a boundary.</summary>
public class NestedFunctionScopeTests
{
    /// <summary>The method whose body holds one read in each kind of scope.</summary>
    private const string Source = """
        void M()
        {
            var plain = first;
            System.Action lambda = () => { var inLambda = second; };
            System.Action anonymous = delegate { var inDelegate = third; };
            void Local() { var inLocal = fourth; }
        }
        """;

    /// <summary>Verifies each nested function kind encloses its read, and the method body does not.</summary>
    /// <param name="name">The identifier read.</param>
    /// <param name="expected">Whether a nested function sits between the read and the method body.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("first", false)]
    [Arguments("second", true)]
    [Arguments("third", true)]
    [Arguments("fourth", true)]
    public async Task NestedFunctionsEncloseTheirReadsAsync(string name, bool expected)
    {
        var method = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(Source)!;
        var read = Find(method, name);

        await Assert.That(NestedFunctionScope.IsInsideNestedFunction(read, method.Body!)).IsEqualTo(expected);
    }

    /// <summary>Verifies a function at or above the boundary does not count.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FunctionAboveTheBoundaryDoesNotCountAsync()
    {
        var method = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(Source)!;
        var read = Find(method, "second");
        var lambdaBody = read.Ancestors().OfType<BlockSyntax>().First();

        await Assert.That(NestedFunctionScope.IsInsideNestedFunction(read, lambdaBody)).IsFalse();
    }

    /// <summary>Finds the identifier with a given name in a method.</summary>
    /// <param name="method">The method to search.</param>
    /// <param name="name">The identifier's name.</param>
    /// <returns>The identifier.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IdentifierNameSyntax Find(MethodDeclarationSyntax method, string name) =>
        method.DescendantNodes().OfType<IdentifierNameSyntax>().First(identifier => identifier.Identifier.ValueText == name);
}
