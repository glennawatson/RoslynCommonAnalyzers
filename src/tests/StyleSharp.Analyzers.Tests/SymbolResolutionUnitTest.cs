// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared single-symbol resolver.</summary>
public sealed class SymbolResolutionUnitTest
{
    /// <summary>The source whose calls the tests resolve.</summary>
    private const string Source =
        """
        class C
        {
            void M(int a) { }
            void Good() { M(1); }
            void Mismatch() { M("x"); }
            void Unknown() { Nope(); }
        }
        """;

    /// <summary>Verifies a cleanly bound call resolves to its symbol.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetSingleSymbolReturnsTheBoundSymbolAsync()
    {
        var symbol = Resolve("Good");

        await Assert.That(symbol).IsNotNull();
        await Assert.That(symbol!.Name).IsEqualTo("M");
    }

    /// <summary>Verifies a failed overload with a single candidate resolves to that candidate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetSingleSymbolFallsBackToTheSoleCandidateAsync()
    {
        var symbol = Resolve("Mismatch");

        await Assert.That(symbol).IsNotNull();
        await Assert.That(symbol!.Name).IsEqualTo("M");
    }

    /// <summary>Verifies an unresolved call yields no symbol.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetSingleSymbolReturnsNullWhenNothingBindsAsync()
    {
        var symbol = Resolve("Unknown");

        await Assert.That(symbol).IsNull();
    }

    /// <summary>Resolves the single symbol for the call inside the named method.</summary>
    /// <param name="methodName">The enclosing method whose single call to resolve.</param>
    /// <returns>The resolved symbol, or <see langword="null"/>.</returns>
    private static ISymbol? Resolve(string methodName)
    {
        var (root, model) = SemanticModelFactory.Create(Source);
        foreach (var member in ((TypeDeclarationSyntax)root.Members[0]).Members)
        {
            if (member is MethodDeclarationSyntax method && method.Identifier.ValueText == methodName)
            {
                var invocation = (InvocationExpressionSyntax)((ExpressionStatementSyntax)method.Body!.Statements[0]).Expression;
                return SymbolResolution.GetSingleSymbol(model.GetSymbolInfo(invocation));
            }
        }

        throw new InvalidOperationException($"Method '{methodName}' was not found.");
    }
}
