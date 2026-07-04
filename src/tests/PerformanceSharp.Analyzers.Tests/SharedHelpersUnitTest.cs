// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>
/// Direct tests for the shared helper sources compiled into both analyzer assemblies:
/// <see cref="DescendantTraversalHelper"/>, <see cref="ModifierListHelper"/>,
/// <see cref="DiagnosticHelper"/>, <see cref="DescriptorFactory"/> (through the descriptors
/// the Rules classes build with it), and <see cref="ImmutableArrays"/>.
/// </summary>
public class SharedHelpersUnitTest
{
    /// <summary>Verifies the token walk visits every token in document order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VisitDescendantTokensSeesEveryTokenInOrderAsync()
    {
        var root = await CSharpSyntaxTree.ParseText("class C { int F; }").GetRootAsync();
        var collected = new List<string>();
        DescendantTraversalHelper.VisitDescendantTokens(
            root,
            ref collected,
            static (in SyntaxToken token, ref List<string> state) =>
            {
                state.Add(token.Text);
                return true;
            });

        await Assert.That(collected).IsEquivalentTo(["class", "C", "{", "int", "F", ";", "}", string.Empty]);
    }

    /// <summary>Verifies returning false stops the token walk immediately.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VisitDescendantTokensStopsWhenTheVisitorSaysSoAsync()
    {
        var root = await CSharpSyntaxTree.ParseText("class C { int F; }").GetRootAsync();
        var count = 0;
        DescendantTraversalHelper.VisitDescendantTokens(
            root,
            ref count,
            static (in SyntaxToken _, ref int state) => ++state < 3);

        await Assert.That(count).IsEqualTo(3);
    }

    /// <summary>Verifies the modifier scan finds present kinds and rejects absent ones.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ModifierContainsFindsOnlyPresentKindsAsync()
    {
        var root = await CSharpSyntaxTree.ParseText("public static class C { }").GetRootAsync();
        var declaration = (ClassDeclarationSyntax)((CompilationUnitSyntax)root).Members[0];

        await Assert.That(ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.StaticKeyword)).IsTrue();
        await Assert.That(ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.SealedKeyword)).IsFalse();
        await Assert.That(ModifierListHelper.ContainsEither(declaration.Modifiers, SyntaxKind.SealedKeyword, SyntaxKind.PublicKeyword)).IsTrue();
        await Assert.That(ModifierListHelper.ContainsEither(declaration.Modifiers, SyntaxKind.SealedKeyword, SyntaxKind.AbstractKeyword)).IsFalse();
    }

    /// <summary>Verifies the factory-built descriptors carry severity, enablement, category, and the docs help link.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DescriptorFactoryBuildsWarningDescriptorsWithHelpLinksAsync()
    {
        var enabled = CollectionRules.UseIsEmpty;
        var optIn = ConcurrencyRules.UseUnsafeRegister;

        await Assert.That(enabled.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(enabled.IsEnabledByDefault).IsTrue();
        await Assert.That(enabled.HelpLinkUri).IsEqualTo("https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/PSH1117.md");
        await Assert.That(enabled.Category).IsEqualTo("Collections");
        await Assert.That(optIn.IsEnabledByDefault).IsFalse();
        await Assert.That(optIn.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Warning);
    }

    /// <summary>Verifies the span-based diagnostic overloads carry location and message arguments through.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiagnosticHelperCarriesSpanAndMessageArgumentsAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { }");
        var span = new TextSpan(6, 1);

        var diagnostic = DiagnosticHelper.Create(ConcurrencyRules.VolatileInterlockedField, tree, span, "first", "second");

        await Assert.That(diagnostic.Id).IsEqualTo("PSH1307");
        await Assert.That(diagnostic.Location.SourceSpan).IsEqualTo(span);
        await Assert.That(diagnostic.GetMessage(null)).IsEqualTo("'first' is an Interlocked target elsewhere in this type; use 'second' for this access");
    }

    /// <summary>Verifies the properties-carrying overload exposes the cached dictionary on the diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiagnosticHelperCarriesPropertiesAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { }");
        var properties = ImmutableDictionary<string, string?>.Empty.Add("key", "value");

        var diagnostic = DiagnosticHelper.Create(ConcurrencyRules.VolatileInterlockedField, tree, new TextSpan(0, 5), properties, "first", "second");

        await Assert.That(diagnostic.Properties["key"]).IsEqualTo("value");
    }

    /// <summary>Verifies the immutable array funnel produces the requested elements in order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImmutableArraysOfBuildsInOrderAsync()
    {
        var two = ImmutableArrays.Of("a", "b");

        await Assert.That(two.Length).IsEqualTo(2);
        await Assert.That(two[0]).IsEqualTo("a");
        await Assert.That(two[1]).IsEqualTo("b");
    }
}
