// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests primitive sizes, sequential padding, and unknown layouts.</summary>
public class StructSizeEstimatorTests
{
    /// <summary>Checks field types are measured consistently and cached for subsequent requests.</summary>
    /// <param name="typeName">The field type to measure.</param>
    /// <param name="expected">The expected size in bytes.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("bool", 1)]
    [Arguments("byte", 1)]
    [Arguments("sbyte", 1)]
    [Arguments("char", 2)]
    [Arguments("short", 2)]
    [Arguments("ushort", 2)]
    [Arguments("int", 4)]
    [Arguments("uint", 4)]
    [Arguments("float", 4)]
    [Arguments("long", 8)]
    [Arguments("ulong", 8)]
    [Arguments("double", 8)]
    [Arguments("decimal", 16)]
    [Arguments("nint", 8)]
    [Arguments("nuint", 8)]
    [Arguments("object", 8)]
    [Arguments("int[]", 8)]
    [Arguments("int*", 8)]
    [Arguments("delegate*<void>", 8)]
    [Arguments("T", -1)]
    [Arguments("Missing", 8)]
    [Arguments("SmallEnum", 2)]
    [Arguments("Empty", 1)]
    [Arguments("Padded", 24)]
    [Arguments("Recursive", -1)]
    [Arguments("Generic<T>", -1)]
    public async Task EstimateReturnsSizeAndCachesResultAsync(string typeName, int expected)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            unsafe class C<T> { public {{typeName}} Value; }
            enum SmallEnum : ushort { Zero }
            struct Empty { public static int Shared; public const int Constant = 1; public void M() {} }
            struct Padded { public byte First; public long Middle; public byte Last; }
            struct Recursive { public Recursive Value; }
            struct Generic<T> { public T Value; }
            """);
        var compilation = CSharpCompilation.Create("Sizes", [tree], RuntimeMetadataReferences.Platform);
        var type = ((IFieldSymbol)compilation.GetTypeByMetadataName("C`1")!.GetMembers("Value")[0]).Type;
        var cache = new ConcurrentDictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);

        await Assert.That(StructSizeEstimator.Estimate(type, cache)).IsEqualTo(expected);
        await Assert.That(StructSizeEstimator.Estimate(type, cache)).IsEqualTo(expected);
        await Assert.That(cache.Count).IsEqualTo(1);
    }
}
