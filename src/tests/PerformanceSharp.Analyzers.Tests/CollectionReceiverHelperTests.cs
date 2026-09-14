// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests count and indexer classification on the receiver's static type.</summary>
public class CollectionReceiverHelperTests
{
    /// <summary>Checks count lookup includes inherited properties and constrained collection interfaces.</summary>
    /// <param name="type">The receiver type.</param>
    /// <param name="declarations">Supporting type declarations and constraints.</param>
    /// <param name="expected">The expected count property, or an empty string when unavailable.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int[]", "", "Length")]
    [Arguments("string", "", "Length")]
    [Arguments("int", "", "")]
    [Arguments("ICollection<int>", "", "Count")]
    [Arguments("IReadOnlyCollection<int>", "", "Count")]
    [Arguments("IEnumerable<int>", "", "")]
    [Arguments("Receiver", "class Receiver { public int Length => 1; }", "Length")]
    [Arguments("Receiver", "class Receiver : Parent { } class Parent { public int Count => 1; }", "Count")]
    [Arguments("Receiver", "class Receiver { public int Count => 1; public int Length => 2; }", "Count")]
    [Arguments("Receiver", "class Receiver { public int Count; }", "")]
    [Arguments("Receiver", "class Receiver { public static int Count => 1; }", "")]
    [Arguments("Receiver", "class Receiver { private int Count => 1; }", "")]
    [Arguments("Receiver", "class Receiver { public long Count => 1; }", "")]
    [Arguments("Receiver", "interface Receiver : ICollection<int> { }", "Count")]
    [Arguments("Receiver", "interface Receiver : IReadOnlyCollection<int> { }", "Count")]
    [Arguments("Receiver", "interface Receiver : IEnumerable<int> { }", "")]
    [Arguments("T", "", "")]
    [Arguments("T", "where T : ICollection<int>", "Count")]
    [Arguments("T", "where T : IReadOnlyCollection<int>", "Count")]
    [Arguments("T", "where T : IList<int>", "Count")]
    [Arguments("T", "where T : IReadOnlyList<int>", "Count")]
    [Arguments("T", "where T : IEnumerable<int>", "")]
    public async Task CountLookupUsesAccessibleStaticTypeMembersAsync(string type, string declarations, string expected)
    {
        var constraint = declarations.StartsWith("where", StringComparison.Ordinal) ? declarations : string.Empty;
        var supportingTypes = constraint.Length == 0 ? declarations : string.Empty;
        var tree = CSharpSyntaxTree.ParseText($"using System.Collections.Generic; class C<T> {constraint} {{ void M({type} value) {{ }} }} {supportingTypes}");
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform);
        var root = await tree.GetRootAsync();
        var parameter = root.DescendantNodes().OfType<ParameterSyntax>().Single();
        var symbol = compilation.GetSemanticModel(tree).GetTypeInfo(parameter.Type!).Type!;

        await Assert.That(CollectionReceiverHelper.TryGetCountSourceName(symbol, out var actual)).IsEqualTo(expected.Length > 0);
        await Assert.That(actual).IsEqualTo(expected);
    }

    /// <summary>Checks framework collection identity supplies Count even when a reduced framework omits the property.</summary>
    /// <param name="name">The framework interface name.</param>
    /// <param name="specialType">The expected recognized framework identity.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("ICollection", SpecialType.System_Collections_Generic_ICollection_T)]
    [Arguments("IReadOnlyCollection", SpecialType.System_Collections_Generic_IReadOnlyCollection_T)]
    public async Task CountInterfaceIdentitySuppliesCountWithoutPropertyAsync(string name, SpecialType specialType)
    {
        var tree = CSharpSyntaxTree.ParseText($"namespace System {{ public class Object {{ }} }} namespace System.Collections.Generic {{ public interface {name}<T> {{ }} }}");
        var compilation = CSharpCompilation.Create(nameof(Test), [tree]);
        var symbol = compilation.GetTypeByMetadataName($"System.Collections.Generic.{name}`1")!;

        await Assert.That(symbol.SpecialType).IsEqualTo(specialType);
        await Assert.That(symbol.GetMembers("Count")).IsEmpty();
        await Assert.That(CollectionReceiverHelper.TryGetCountSourceName(symbol, out var property)).IsTrue();
        await Assert.That(property).IsEqualTo("Count");
    }

    /// <summary>Checks list lookup distinguishes array ranks, direct interfaces, and inherited interfaces.</summary>
    /// <param name="type">The receiver type.</param>
    /// <param name="expected">Whether indexed access is supported.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int[]", true)]
    [Arguments("int[,]", false)]
    [Arguments("string", true)]
    [Arguments("IList<int>", true)]
    [Arguments("IReadOnlyList<int>", true)]
    [Arguments("List<int>", true)]
    [Arguments("ReadOnlyList", true)]
    [Arguments("IEnumerable<int>", false)]
    [Arguments("HashSet<int>", false)]
    [Arguments("int", false)]
    public async Task ListLookupRespectsRankAndInterfacesAsync(string type, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"using System.Collections.Generic; class C {{ void M({type} value) {{ }} }} interface ReadOnlyList : IReadOnlyList<int> {{ }}");
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform);
        var root = await tree.GetRootAsync();
        var parameter = root.DescendantNodes().OfType<ParameterSyntax>().Single();
        var symbol = compilation.GetSemanticModel(tree).GetTypeInfo(parameter.Type!).Type!;

        await Assert.That(CollectionReceiverHelper.IsListLike(symbol)).IsEqualTo(expected);
    }
}
