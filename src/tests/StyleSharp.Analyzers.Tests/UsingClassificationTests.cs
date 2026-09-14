// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the canonical ordering and fallback keys of using directives.</summary>
public class UsingClassificationTests
{
    /// <summary>Checks group, System recognition, and sort keys independently.</summary>
    /// <param name="source">The directive to classify.</param>
    /// <param name="group">The expected ordering group.</param>
    /// <param name="system">Whether the target starts with System.</param>
    /// <param name="key">The alphabetical key.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System;", 0, true, "System")]
    [Arguments("using System.Text;", 0, true, "System.Text")]
    [Arguments("using Systematic;", 0, false, "Systematic")]
    [Arguments("using System::Text;", 0, true, "System::Text")]
    [Arguments("using global::System;", 0, false, "global::System")]
    [Arguments("using static System.Math;", 1, true, "System.Math")]
    [Arguments("using @Alias = System.Text;", 2, true, "Alias")]
    public async Task DirectiveClassificationUsesItsSyntaxAsync(string source, int group, bool system, string key)
    {
        var directive = Parse(source);
        await Assert.That(UsingClassification.Group(directive)).IsEqualTo(group);
        await Assert.That(UsingClassification.IsSystem(directive)).IsEqualTo(system);
        await Assert.That(UsingClassification.SortKey(directive)).IsEqualTo(key);
    }

    /// <summary>Checks ordering in both directions, including equal and fallback names.</summary>
    /// <param name="left">The first directive.</param>
    /// <param name="right">The second directive.</param>
    /// <param name="expected">The sign of the comparison.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using A;", "using B;", -1)]
    [Arguments("using A;", "using A;", 0)]
    [Arguments("using A.B;", "using A.B;", 0)]
    [Arguments("using A.B;", "using A.C;", -1)]
    [Arguments("using A.B.C;", "using A.C.D;", -1)]
    [Arguments("using A.B.C;", "using A.B;", 1)]
    [Arguments("using A.B.C;", "using B.C;", -1)]
    [Arguments("using System;", "using A;", -1)]
    [Arguments("using A;", "using static A;", -1)]
    [Arguments("using static A;", "using A = A;", -1)]
    [Arguments("using A = Z;", "using B = A;", -1)]
    [Arguments("using global::A;", "using A;", 1)]
    public async Task CanonicalComparisonIsAntisymmetricAsync(string left, string right, int expected)
    {
        var first = Parse(left);
        var second = Parse(right);
        await Assert.That(Math.Sign(UsingClassification.Compare(first, second))).IsEqualTo(expected);
        await Assert.That(Math.Sign(UsingClassification.Compare(second, first))).IsEqualTo(-expected);
    }

    /// <summary>Checks sort-key fallback when only one side is an alias.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MixedAliasSortKeysUseTheAvailableNameAsync()
    {
        var alias = Parse("using Z = global::A;");
        var regular = Parse("using A;");
        await Assert.That(UsingClassification.CompareSortKey(alias, regular)).IsGreaterThan(0);
        await Assert.That(UsingClassification.CompareSortKey(regular, alias)).IsLessThan(0);
    }

    /// <summary>Checks non-name targets have no namespace key and use the fallback comparison.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NonNameTargetHasAnEmptySortKeyAsync()
    {
        var named = Parse("using static A;");
        var directive = named.WithNamespaceOrType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)));
        await Assert.That(UsingClassification.IsSystem(directive)).IsFalse();
        await Assert.That(UsingClassification.SortKey(directive)).IsEmpty();
        await Assert.That(UsingClassification.Compare(directive, named)).IsLessThan(0);
        await Assert.That(UsingClassification.Compare(named, directive)).IsGreaterThan(0);
        await Assert.That(UsingClassification.CompareSortKey(directive, directive)).IsEqualTo(0);
    }

    /// <summary>Parses the directive without requiring its target to bind.</summary>
    /// <param name="source">The directive text.</param>
    /// <returns>The parsed directive.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UsingDirectiveSyntax Parse(string source) => SyntaxFactory.ParseCompilationUnit(source).Usings[0];
}
