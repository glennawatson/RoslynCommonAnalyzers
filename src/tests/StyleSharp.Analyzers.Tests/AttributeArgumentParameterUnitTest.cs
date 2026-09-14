// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared attribute argument to constructor parameter mapping.</summary>
public sealed class AttributeArgumentParameterUnitTest
{
    /// <summary>The positional arguments the constructor declares parameters for.</summary>
    private const int ConstructorParameterCount = 2;

    /// <summary>The cursor after an extra positional argument beyond the constructor's parameters.</summary>
    private const int CursorPastExtraArgument = 3;

    /// <summary>Source declaring an attribute with two constructor parameters and one applied use of it.</summary>
    private const string Source =
        """
        using System;
        sealed class RouteAttribute : Attribute
        {
            public RouteAttribute(string template, int order) { }
            public string Name { get; set; }
        }
        [Route(Name = "n", order: 2, template: "t")]
        class Named { }
        [Route("t", 2, Name = "n")]
        class Positional { }
        [Route("t", 2, 3)]
        class Extra { }
        """;

    /// <summary>Verifies named arguments map by name and property assignments map to nothing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedArgumentsMapByNameAsync()
    {
        var (arguments, constructor) = Bind("Named");
        var positional = 0;

        await Assert.That(AttributeArgumentParameter.NameOf(arguments[0], constructor, ref positional)).IsNull();
        await Assert.That(AttributeArgumentParameter.NameOf(arguments[1], constructor, ref positional)).IsEqualTo("order");
        await Assert.That(AttributeArgumentParameter.NameOf(arguments[2], constructor, ref positional)).IsEqualTo("template");
        await Assert.That(positional).IsEqualTo(0);
    }

    /// <summary>Verifies positional arguments map in order and a trailing property assignment leaves the cursor alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PositionalArgumentsMapInOrderAsync()
    {
        var (arguments, constructor) = Bind("Positional");
        var positional = 0;

        await Assert.That(AttributeArgumentParameter.NameOf(arguments[0], constructor, ref positional)).IsEqualTo("template");
        await Assert.That(AttributeArgumentParameter.NameOf(arguments[1], constructor, ref positional)).IsEqualTo("order");
        await Assert.That(AttributeArgumentParameter.NameOf(arguments[2], constructor, ref positional)).IsNull();
        await Assert.That(positional).IsEqualTo(ConstructorParameterCount);
    }

    /// <summary>Verifies a positional argument beyond the constructor's parameters maps to nothing but still advances.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentBeyondTheParametersMapsToNothingAsync()
    {
        var (arguments, constructor) = Bind("Extra");
        var positional = ConstructorParameterCount;

        await Assert.That(AttributeArgumentParameter.NameOf(arguments[ConstructorParameterCount], constructor, ref positional)).IsNull();
        await Assert.That(positional).IsEqualTo(CursorPastExtraArgument);
    }

    /// <summary>Binds the attribute applied to a class and returns its arguments and constructor.</summary>
    /// <param name="className">The attributed class.</param>
    /// <returns>The attribute's arguments and the constructor that attribute binds to.</returns>
    private static BoundAttribute Bind(string className)
    {
        var tree = CSharpSyntaxTree.ParseText(Source);
        var compilation = CSharpCompilation.Create(nameof(AttributeArgumentParameterUnitTest), [tree], [RuntimeMetadataReferences.CoreLibrary]);
        var model = compilation.GetSemanticModel(tree);
        var attribute = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(type => type.Identifier.ValueText == className)
            .AttributeLists[0].Attributes[0];
        var info = model.GetSymbolInfo(attribute);

        // An argument list with no matching overload leaves the constructor as the only candidate rather than the bound symbol.
        var constructor = (IMethodSymbol)(info.Symbol ?? info.CandidateSymbols[0]);
        return new(attribute.ArgumentList!.Arguments, constructor);
    }

    /// <summary>An applied attribute's written arguments and its bound constructor.</summary>
    /// <param name="Arguments">The written arguments.</param>
    /// <param name="Constructor">The bound constructor.</param>
    private readonly record struct BoundAttribute(SeparatedSyntaxList<AttributeArgumentSyntax> Arguments, IMethodSymbol Constructor);
}
