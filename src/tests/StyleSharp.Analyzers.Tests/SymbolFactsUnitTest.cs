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
    /// <summary>The metadata name of the marker attribute class.</summary>
    private const string MarkerAttributeName = "MarkerAttribute";

    /// <summary>The source whose symbols the tests read.</summary>
    private const string Source =
        """
        using System;

        class MarkerAttribute : Attribute { }
        class DerivedMarkerAttribute : MarkerAttribute { }
        class OtherAttribute : Attribute { }

        [Marker] class Marked { }
        [DerivedMarker] class DerivedMarked { }
        class InheritsMarked : Marked { }

        [Other]
        class Unmarked
        {
            public int Count;
            public int Size { get; set; }
            public void Run() { }
            public int Run(int value) => value;
        }

        class Probe
        {
            public static void A() { }
            public void B() { }
            public static void C(int x) { }
            public static void C(int x, int y) { }
            public void D(int x) { }
            public static int E;
            public int Instance;
            public static int Total { get; set; }
            public int InstanceSize { get; }
            public override bool Equals(object obj) => false;
            public override int GetHashCode() => 0;
        }
        """;

    /// <summary>The compilation every test binds against.</summary>
    private static readonly CSharpCompilation Compilation = CSharpCompilation.Create(
        nameof(SymbolFactsUnitTest),
        [CSharpSyntaxTree.ParseText(Source)],
        RuntimeMetadataReferences.Platform);

    /// <summary>The marker attribute class.</summary>
    private static readonly INamedTypeSymbol MarkerType = Compilation.GetTypeByMetadataName(MarkerAttributeName)!;

    /// <summary>A class carrying the marker attribute itself.</summary>
    private static readonly INamedTypeSymbol MarkedType = Compilation.GetTypeByMetadataName("Marked")!;

    /// <summary>A class carrying an attribute derived from the marker.</summary>
    private static readonly INamedTypeSymbol DerivedMarkedType = Compilation.GetTypeByMetadataName("DerivedMarked")!;

    /// <summary>A class whose base class carries the marker attribute.</summary>
    private static readonly INamedTypeSymbol InheritsMarkedType = Compilation.GetTypeByMetadataName("InheritsMarked")!;

    /// <summary>A class carrying an unrelated attribute, with a field, a property and an overloaded method.</summary>
    private static readonly INamedTypeSymbol UnmarkedType = Compilation.GetTypeByMetadataName("Unmarked")!;

    /// <summary>A class declaring static, instance and override methods.</summary>
    private static readonly INamedTypeSymbol ProbeType = Compilation.GetTypeByMetadataName("Probe")!;

    /// <summary>Verifies the exact attribute match ignores a derived attribute class.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasAttributeMatchesTheExactClassAsync()
    {
        await Assert.That(SymbolFacts.HasAttribute(MarkedType.GetAttributes(), MarkerType)).IsTrue();
        await Assert.That(SymbolFacts.HasAttribute(DerivedMarkedType.GetAttributes(), MarkerType)).IsFalse();
        await Assert.That(SymbolFacts.HasAttribute(UnmarkedType.GetAttributes(), MarkerType)).IsFalse();
    }

    /// <summary>Verifies the name match reads the attribute class's own name and ignores its base classes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasAttributeNamedMatchesTheClassNameAsync()
    {
        await Assert.That(SymbolFacts.HasAttributeNamed(MarkedType.GetAttributes(), MarkerAttributeName)).IsTrue();
        await Assert.That(SymbolFacts.HasAttributeNamed(DerivedMarkedType.GetAttributes(), MarkerAttributeName)).IsFalse();
        await Assert.That(SymbolFacts.HasAttributeNamed(UnmarkedType.GetAttributes(), MarkerAttributeName)).IsFalse();
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

    /// <summary>Verifies the hierarchy match finds a marker on a base class and a derived marker on the type itself.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasAttributeDerivedFromInHierarchyWalksBaseTypesAsync()
    {
        await Assert.That(SymbolFacts.HasAttributeDerivedFromInHierarchy(InheritsMarkedType, MarkerType)).IsTrue();
        await Assert.That(SymbolFacts.HasAttributeDerivedFromInHierarchy(DerivedMarkedType, MarkerType)).IsTrue();
        await Assert.That(SymbolFacts.HasAttributeDerivedFromInHierarchy(UnmarkedType, MarkerType)).IsFalse();
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

    /// <summary>Verifies only a static method of the name counts, whatever its arity.</summary>
    /// <param name="name">The probed name.</param>
    /// <param name="expected">Whether a static method of the name exists.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("A", true)]
    [Arguments("B", false)]
    [Arguments("C", true)]
    [Arguments("E", false)]
    [Arguments("Absent", false)]
    public async Task HasStaticMethodAsync(string name, bool expected) =>
        await Assert.That(SymbolFacts.HasStaticMethod(ProbeType, name)).IsEqualTo(expected);

    /// <summary>Verifies the arity overload also requires the parameter count to match.</summary>
    /// <param name="name">The probed name.</param>
    /// <param name="parameterCount">The required parameter count.</param>
    /// <param name="expected">Whether a static method of the name and arity exists.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("A", 0, true)]
    [Arguments("A", 1, false)]
    [Arguments("C", 2, true)]
    [Arguments("D", 1, false)]
    [Arguments("Absent", 0, false)]
    public async Task HasStaticMethodWithArityAsync(string name, int parameterCount, bool expected) =>
        await Assert.That(SymbolFacts.HasStaticMethod(ProbeType, name, parameterCount)).IsEqualTo(expected);

    /// <summary>Verifies only a static field of the name counts.</summary>
    /// <param name="name">The probed name.</param>
    /// <param name="expected">Whether a static field of the name exists.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("E", true)]
    [Arguments("Instance", false)]
    [Arguments("Total", false)]
    [Arguments("A", false)]
    [Arguments("Absent", false)]
    public async Task HasStaticFieldAsync(string name, bool expected) =>
        await Assert.That(SymbolFacts.HasStaticField(ProbeType, name)).IsEqualTo(expected);

    /// <summary>Verifies only a static property of the name counts.</summary>
    /// <param name="name">The probed name.</param>
    /// <param name="expected">Whether a static property of the name exists.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("Total", true)]
    [Arguments("InstanceSize", false)]
    [Arguments("E", false)]
    [Arguments("A", false)]
    [Arguments("Absent", false)]
    public async Task HasStaticPropertyAsync(string name, bool expected) =>
        await Assert.That(SymbolFacts.HasStaticProperty(ProbeType, name)).IsEqualTo(expected);

    /// <summary>Verifies an override counts only when declared on the type with the requested arity.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasOverrideRequiresADeclaredOverrideAsync()
    {
        await Assert.That(SymbolFacts.HasOverride(ProbeType, nameof(Equals), parameterCount: 1)).IsTrue();
        await Assert.That(SymbolFacts.HasOverride(ProbeType, nameof(GetHashCode), parameterCount: 0)).IsTrue();
        await Assert.That(SymbolFacts.HasOverride(ProbeType, nameof(Equals), parameterCount: 0)).IsFalse();
        await Assert.That(SymbolFacts.HasOverride(UnmarkedType, nameof(Equals), parameterCount: 1)).IsFalse();
    }

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
