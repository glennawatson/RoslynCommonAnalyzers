// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the comparison of a bound method with the other overloads in its method group.</summary>
public class SiblingOverloadsTests
{
    /// <summary>The overload set every test compares against <c>Use(string, int)</c>.</summary>
    private const string OverloadSource =
        """
        class C
        {
            int Other;
            void Use(string a, int b) { }
            void Use(object a, int b) { }
            void Use(string a, long b) { }
            static void Use(char a, int b) { }
            void Use<T>(T a, int b) { }
            void Use(string a) { }
        }
        """;

    /// <summary>The display of the bound method every candidate is compared with.</summary>
    private const string BoundMethodDisplay = "C.Use(string, int)";

    /// <summary>Verifies a sibling must be a different, non-generic method with the same staticness and parameter count.</summary>
    /// <param name="display">The candidate member's display string.</param>
    /// <param name="expected">Whether the candidate is a same-shape sibling.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(BoundMethodDisplay, false)]
    [Arguments("C.Use(object, int)", true)]
    [Arguments("C.Use(string, long)", true)]
    [Arguments("C.Use(char, int)", false)]
    [Arguments("C.Use<T>(T, int)", false)]
    [Arguments("C.Use(string)", false)]
    [Arguments("C.Other", false)]
    public async Task SiblingMustShareShapeWithTheMethodAsync(string display, bool expected)
    {
        var type = CreateType();
        var method = FindMember<IMethodSymbol>(type, BoundMethodDisplay);

        await Assert.That(SiblingOverloads.IsSameShapeSibling(FindMember<ISymbol>(type, display), method, out var sibling)).IsEqualTo(expected);
        await Assert.That(sibling is not null).IsEqualTo(expected);
    }

    /// <summary>Verifies parameter types must match everywhere except the named position.</summary>
    /// <param name="display">The candidate overload's display string.</param>
    /// <param name="index">The position allowed to differ.</param>
    /// <param name="expected">Whether the remaining parameter types match.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("C.Use(object, int)", 0, true)]
    [Arguments("C.Use(object, int)", 1, false)]
    [Arguments("C.Use(string, long)", 1, true)]
    [Arguments("C.Use(string, long)", 0, false)]
    [Arguments(BoundMethodDisplay, 0, true)]
    public async Task ParameterTypesMatchEverywhereButTheSlotAsync(string display, int index, bool expected)
    {
        var type = CreateType();
        var method = FindMember<IMethodSymbol>(type, BoundMethodDisplay);

        await Assert.That(SiblingOverloads.ParameterTypesMatchExcept(FindMember<IMethodSymbol>(type, display), method, index)).IsEqualTo(expected);
    }

    /// <summary>Compiles the overload set and returns its type.</summary>
    /// <returns>The declared type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static INamedTypeSymbol CreateType() =>
        CSharpCompilation.Create(nameof(SiblingOverloadsTests), [CSharpSyntaxTree.ParseText(OverloadSource)], [RuntimeMetadataReferences.CoreLibrary])
            .GetTypeByMetadataName("C")!;

    /// <summary>Finds a member by its display string.</summary>
    /// <typeparam name="TSymbol">The member's symbol type.</typeparam>
    /// <param name="type">The declaring type.</param>
    /// <param name="display">The display string.</param>
    /// <returns>The member.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TSymbol FindMember<TSymbol>(INamedTypeSymbol type, string display)
        where TSymbol : ISymbol =>
        type.GetMembers().OfType<TSymbol>().Single(member => string.Equals(member.ToDisplayString(), display, StringComparison.Ordinal));
}
