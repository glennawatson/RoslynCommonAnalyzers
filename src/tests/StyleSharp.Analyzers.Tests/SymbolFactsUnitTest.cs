// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared reads of what a bound symbol carries.</summary>
public sealed class SymbolFactsUnitTest
{
    /// <summary>The source whose symbols the tests read.</summary>
    private const string Source =
        """
        using System;

        class MarkerAttribute : Attribute { }
        class DerivedMarkerAttribute : MarkerAttribute { }
        class OtherAttribute : Attribute { }

        [Marker] class Marked { }
        [DerivedMarker] class DerivedMarked { }

        [Other]
        class Unmarked
        {
            public int Count;
            public int Size { get; set; }
            public void Run() { }
            public int Run(int value) => value;
        }
        """;

    /// <summary>The compilation every test binds against.</summary>
    private static readonly CSharpCompilation Compilation = CSharpCompilation.Create(
        nameof(SymbolFactsUnitTest),
        [CSharpSyntaxTree.ParseText(Source)],
        RuntimeMetadataReferences.Platform);

    /// <summary>The marker attribute class.</summary>
    private static readonly INamedTypeSymbol MarkerType = Compilation.GetTypeByMetadataName("MarkerAttribute")!;

    /// <summary>A class carrying the marker attribute itself.</summary>
    private static readonly INamedTypeSymbol MarkedType = Compilation.GetTypeByMetadataName("Marked")!;

    /// <summary>A class carrying an attribute derived from the marker.</summary>
    private static readonly INamedTypeSymbol DerivedMarkedType = Compilation.GetTypeByMetadataName("DerivedMarked")!;

    /// <summary>A class carrying an unrelated attribute, with a field, a property and an overloaded method.</summary>
    private static readonly INamedTypeSymbol UnmarkedType = Compilation.GetTypeByMetadataName("Unmarked")!;

    /// <summary>Verifies the exact attribute match ignores a derived attribute class.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasAttributeMatchesTheExactClassAsync()
    {
        await Assert.That(SymbolFacts.HasAttribute(MarkedType.GetAttributes(), MarkerType)).IsTrue();
        await Assert.That(SymbolFacts.HasAttribute(DerivedMarkedType.GetAttributes(), MarkerType)).IsFalse();
        await Assert.That(SymbolFacts.HasAttribute(UnmarkedType.GetAttributes(), MarkerType)).IsFalse();
    }

    /// <summary>Verifies the derived attribute match accepts a subclass and rejects an empty marker set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasAttributeDerivedFromAnyAcceptsSubclassesAsync()
    {
        INamedTypeSymbol[] markers = [MarkerType];

        await Assert.That(SymbolFacts.HasAttributeDerivedFromAny(DerivedMarkedType.GetAttributes(), markers)).IsTrue();
        await Assert.That(SymbolFacts.HasAttributeDerivedFromAny(MarkedType.GetAttributes(), markers)).IsTrue();
        await Assert.That(SymbolFacts.HasAttributeDerivedFromAny(UnmarkedType.GetAttributes(), markers)).IsFalse();
        await Assert.That(SymbolFacts.HasAttributeDerivedFromAny(MarkedType.GetAttributes(), [])).IsFalse();
    }

    /// <summary>Verifies only a method of the name counts, whatever its overloads.</summary>
    /// <param name="name">The member name.</param>
    /// <param name="expected">Whether a method of that name is declared.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("Run", true)]
    [Arguments("Count", false)]
    [Arguments("Size", false)]
    [Arguments("Missing", false)]
    public async Task HasMethodNamedAsync(string name, bool expected) =>
        await Assert.That(SymbolFacts.HasMethodNamed(UnmarkedType, name)).IsEqualTo(expected);

    /// <summary>Verifies a type's declaring syntax is matched by kind.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsDeclaredAsMatchesTheDeclaringSyntaxAsync()
    {
        await Assert.That(SymbolFacts.IsDeclaredAs<ClassDeclarationSyntax>(UnmarkedType, CancellationToken.None)).IsTrue();
        await Assert.That(SymbolFacts.IsDeclaredAs<CompilationUnitSyntax>(UnmarkedType, CancellationToken.None)).IsFalse();
    }

    /// <summary>Verifies the type top-level statements generate is declared by its compilation unit.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TopLevelStatementsProgramIsDeclaredByTheCompilationUnitAsync()
    {
        var compilation = CSharpCompilation.Create(
            nameof(TopLevelStatementsProgramIsDeclaredByTheCompilationUnitAsync),
            [CSharpSyntaxTree.ParseText("System.Console.WriteLine();")],
            RuntimeMetadataReferences.Platform,
            new(OutputKind.ConsoleApplication));
        var program = compilation.GetTypeByMetadataName("Program")!;

        await Assert.That(SymbolFacts.IsDeclaredAs<CompilationUnitSyntax>(program, CancellationToken.None)).IsTrue();
    }
}
