// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Tests the mapping from a parameter to the argument that supplies it.</summary>
public class ArgumentLookupTests
{
    /// <summary>The parameter every lookup asks for.</summary>
    private const string ParameterName = "second";

    /// <summary>Verifies a naming argument wins and a positional argument only counts when it names nothing else.</summary>
    /// <param name="call">The invocation text.</param>
    /// <param name="ordinal">The parameter's position.</param>
    /// <param name="expected">The supplying argument's expression text, or an empty string when none is identified.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("M(1, 2)", 1, "2")]
    [Arguments("M(1, second: 2)", 1, "2")]
    [Arguments("M(second: 2, first: 1)", 1, "2")]
    [Arguments("M(first: 1, 2)", 1, "2")]
    [Arguments("M(1, other: 2)", 1, "")]
    [Arguments("M(1)", 1, "")]
    [Arguments("M()", 0, "")]
    [Arguments("M(1, 2)", -1, "")]
    public async Task CallArgumentHonoursNamesBeforePositionAsync(string call, int ordinal, string expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(call);

        await Assert.That(ArgumentLookup.Find(invocation.ArgumentList.Arguments, ParameterName, ordinal)?.Expression.ToString() ?? string.Empty)
            .IsEqualTo(expected);
    }

    /// <summary>Verifies an attribute's property assignment never supplies a constructor parameter.</summary>
    /// <param name="attribute">The attribute text.</param>
    /// <param name="ordinal">The constructor parameter's position.</param>
    /// <param name="expected">The supplying argument's expression text, or an empty string when none is identified.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("[A(1, 2)]", 1, "2")]
    [Arguments("[A(1, second: 2)]", 1, "2")]
    [Arguments("[A(1, other: 2)]", 1, "")]
    [Arguments("[A(1, Second = 2)]", 1, "")]
    [Arguments("[A(1)]", 1, "")]
    [Arguments("[A(1, 2)]", -1, "")]
    public async Task AttributeArgumentSkipsPropertyAssignmentsAsync(string attribute, int ordinal, string expected)
    {
        var syntax = SyntaxFactory.ParseCompilationUnit($"{attribute} class C {{ }}").DescendantNodes().OfType<AttributeSyntax>().Single();

        await Assert.That(ArgumentLookup.Find(syntax.ArgumentList!.Arguments, ParameterName, ordinal)?.Expression.ToString() ?? string.Empty)
            .IsEqualTo(expected);
    }

    /// <summary>Verifies the parameter's own position in the bound method decides the positional fallback.</summary>
    /// <param name="call">The invocation text.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="expected">The supplying argument's expression text, or an empty string when none is identified.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("M(1, \"a\")", ParameterName, "\"a\"")]
    [Arguments("M(second: \"a\", first: 1)", ParameterName, "\"a\"")]
    [Arguments("M(1, \"a\")", "first", "1")]
    [Arguments("M(1, \"a\")", "missing", "")]
    [Arguments("M(1)", ParameterName, "")]
    public async Task ArgumentForParameterUsesTheBoundPositionAsync(string call, string name, string expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(call);

        await Assert.That(ArgumentLookup.FindForParameter(invocation.ArgumentList.Arguments, CreateMethod(), name)?.Expression.ToString() ?? string.Empty)
            .IsEqualTo(expected);
    }

    /// <summary>Verifies a parameter's position is found by name, and a missing name has none.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="expected">The expected position.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("first", 0)]
    [Arguments(ParameterName, 1)]
    [Arguments("missing", -1)]
    public async Task ParameterPositionIsFoundByNameAsync(string name, int expected) =>
        await Assert.That(ArgumentLookup.IndexOfParameter(CreateMethod(), name)).IsEqualTo(expected);

    /// <summary>Compiles <c>M(int first, string second)</c> and returns its symbol.</summary>
    /// <returns>The method symbol.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IMethodSymbol CreateMethod() =>
        CSharpCompilation.Create(nameof(ArgumentLookupTests), [CSharpSyntaxTree.ParseText("class C { void M(int first, string second) { } }")], [RuntimeMetadataReferences.CoreLibrary])
            .GetTypeByMetadataName("C")!
            .GetMembers("M")
            .OfType<IMethodSymbol>()
            .Single();
}
