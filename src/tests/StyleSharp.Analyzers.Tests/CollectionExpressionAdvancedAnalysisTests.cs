// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests collection-expression syntax matching and semantic factory constraints.</summary>
public class CollectionExpressionAdvancedAnalysisTests
{
    /// <summary>Checks initializer recognition for every supported allocation shape.</summary>
    /// <param name="source">The allocation expression.</param>
    /// <param name="inline">Whether an initializer exists.</param>
    /// <param name="stack">Whether it is a stack allocation initializer.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("new int[] { 1, 2 }", true, false)]
    [Arguments("new[] { 1, 2 }", true, false)]
    [Arguments("stackalloc int[] { 1, 2 }", true, true)]
    [Arguments("stackalloc[] { 1, 2 }", true, true)]
    [Arguments("new int[2]", false, false)]
    [Arguments("stackalloc int[2]", false, false)]
    [Arguments("values", false, false)]
    [Arguments("new[] { 1,", false, false)]
    [Arguments("new int[] {", false, false)]
    [Arguments("stackalloc[] { 1,", false, false)]
    [Arguments("stackalloc int[] {", false, false)]
    public async Task InitializerRecognitionDistinguishesAllocationShapesAsync(string source, bool inline, bool stack)
    {
        var expression = SyntaxFactory.ParseExpression(source);
        await Assert.That(CollectionExpressionAdvancedAnalysis.TryGetInlineInitializer(expression, out var initializer)).IsEqualTo(inline);
        await Assert.That(CollectionExpressionAdvancedAnalysis.TryGetStackallocInitializer(expression, out _)).IsEqualTo(stack);
        if (inline)
        {
            await Assert.That(CollectionExpressionAdvancedAnalysis.CollectionExpressionText(initializer)).IsEqualTo("[ 1, 2 ]");
        }
    }

    /// <summary>Checks empty factories, element factories, range spreads, and unsupported invocations.</summary>
    /// <param name="source">The invocation.</param>
    /// <param name="expected">The replacement, or null when unsupported.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("Create(1)", null)]
    [Arguments("Factory.Other(1)", null)]
    [Arguments("Factory.Create()", "[]")]
    [Arguments("Factory.Create(1)", "[1]")]
    [Arguments("Factory.Create(1, 2)", "[1, 2]")]
    [Arguments("Factory.Create(new[] { 1, 2 })", "[ 1, 2 ]")]
    [Arguments("Factory.CreateRange(values)", "[.. values]")]
    [Arguments("Factory.CreateRange()", "[]")]
    [Arguments("Factory.CreateRange(first, second)", "[first, second]")]
    [Arguments("new[] { 1, 2 }.ToList()", "[ 1, 2 ]")]
    [Arguments("new[] { 1, 2 }.ToArray()", "[ 1, 2 ]")]
    [Arguments("values.ToArray()", null)]
    [Arguments("new[] { 1 }.ToArray(1)", null)]
    [Arguments("Factory.Create(", null)]
    [Arguments("Factory.Create(1,)", null)]
    [Arguments("Factory.CreateRange(values", null)]
    [Arguments("new[] { 1 }.ToArray(", null)]
    public async Task InvocationReplacementPreservesElementsAsync(string source, string? expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(source);
        await Assert.That(CollectionExpressionAdvancedAnalysis.TryBuildInvocationCollectionExpression(invocation, out var text)).IsEqualTo(expected is not null);
        await Assert.That(text).IsEqualTo(expected ?? string.Empty);
    }

    /// <summary>Checks builder sequences require a single initialized local and contiguous additions followed by materialization.</summary>
    /// <param name="source">The containing block.</param>
    /// <param name="expected">Whether the sequence matches.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("{ var b = F.CreateBuilder(); b.Add(1); return b.ToImmutable(); }", true)]
    [Arguments("{ Log(); var b = F.GetInstance(); b.Add(1); return b.ToArrayAndFree(); Log(); }", true)]
    [Arguments("{ var b = F.GetInstance(); b.Add(1); return b.ToImmutableAndClear(); }", true)]
    [Arguments("{ var b = F.GetInstance(); b.Add(1); return b.ToImmutableAndFree(); }", true)]
    [Arguments("{ var b = F.GetInstance(); b.Add(1); return b.ToArray(); }", true)]
    [Arguments("{ var b = F.GetInstance(); }", false)]
    [Arguments("{ var b = F.GetInstance(); return b.ToArray(); }", false)]
    [Arguments("{ var b = F.GetInstance(); return b.ToArray(); Log(); }", false)]
    [Arguments("{ var b = F.GetInstance(); b.Add(1); return other.ToArray(); }", false)]
    [Arguments("{ var b = F.GetInstance(); b.Add(1); return b.Other(); }", false)]
    [Arguments("{ var b = F.GetInstance(); b.Add(1); return b.ToArray(1); }", false)]
    [Arguments("{ var b = F.GetInstance(); b.Add(1); return ToArray(); }", false)]
    [Arguments("{ var b = F.GetInstance(); b.Add(1); return this.ToArray(); }", false)]
    [Arguments("{ var b = F.GetInstance(); b.Add(1); return; }", false)]
    [Arguments("{ var b = F.GetInstance(); other.Add(1); return b.ToArray(); }", false)]
    [Arguments("{ var b = F.GetInstance(); b.Remove(1); return b.ToArray(); }", false)]
    [Arguments("{ var b = F.GetInstance(); b.Add(); return b.ToArray(); }", false)]
    [Arguments("{ var b = F.GetInstance(); this.Add(1); return b.ToArray(); }", false)]
    [Arguments("{ var b = F.GetInstance(); Add(1); return b.ToArray(); }", false)]
    [Arguments("{ var b = F.GetInstance(); b = other; return b.ToArray(); }", false)]
    [Arguments("{ var b = F.GetInstance(); if (flag) b.Add(1); return b.ToArray(); }", false)]
    [Arguments("{ var b = F.GetInstance(), c = F.GetInstance(); b.Add(1); return b.ToArray(); }", false)]
    [Arguments("{ Builder b; b.Add(1); return b.ToArray(); }", false)]
    [Arguments("{ var b = 1; b.Add(1); return b.ToArray(); }", false)]
    [Arguments("{ var b = F.Other(); b.Add(1); return b.ToArray(); }", false)]
    [Arguments("{ var b = CreateBuilder(); b.Add(1); return b.ToArray(); }", false)]
    public async Task BuilderSequenceRequiresContiguousSupportedStatementsAsync(string source, bool expected)
    {
        var block = (BlockSyntax)SyntaxFactory.ParseStatement(source);
        var local = block.Statements.OfType<LocalDeclarationStatementSyntax>().First();
        await Assert.That(CollectionExpressionAdvancedAnalysis.TryGetBuilderSequence(local, out var elements, out var terminal)).IsEqualTo(expected);
        await Assert.That(elements.Length).IsEqualTo(expected ? 1 : 0);
        if (!expected)
        {
            return;
        }

        await Assert.That(elements[0].ToString()).IsEqualTo("1");
        await Assert.That(terminal).IsNotNull();
    }

    /// <summary>Checks a builder declaration outside a block is rejected.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task DetachedBuilderLocalIsRejectedAsync()
    {
        var local = (LocalDeclarationStatementSyntax)SyntaxFactory.ParseStatement("var b = F.CreateBuilder();");
        await Assert.That(CollectionExpressionAdvancedAnalysis.TryGetBuilderSequence(local, out _, out _)).IsFalse();
    }

    /// <summary>Verifies unfinished builder sequences are rejected without throwing or exposing partial results.</summary>
    /// <param name="source">The unfinished or malformed builder block.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("{ var b = F.CreateBuilder(); b.Add(1); b.Add(2); }")]
    [Arguments("{ var b = F.CreateBuilder(); b.Add(1); }")]
    [Arguments("{ var b = F.CreateBuilder(); b.Add(1); b.Add(2);")]
    [Arguments("{ var b = F.CreateBuilder(); b.Add(")]
    [Arguments("{ var b = F.CreateBuilder(); b.Add(1); return b.ToArray(")]
    [Arguments("{ var b = F.CreateBuilder(); b.Add(1); return b.ToArray() }")]
    [Arguments("{ var b = F.CreateBuilder(); b.Add(1) return b.ToArray(); }")]
    [Arguments("{ var b = F.CreateBuilder(; b.Add(1); return b.ToArray(); }")]
    public async Task UnterminatedBuilderSequenceIsRejectedAsync(string source)
    {
        var block = (BlockSyntax)SyntaxFactory.ParseStatement(source);
        var local = (LocalDeclarationStatementSyntax)block.Statements[0];
        await Assert.That(CollectionExpressionAdvancedAnalysis.TryGetBuilderSequence(local, out var elements, out var terminal)).IsFalse();
        await Assert.That(elements).IsEmpty();
        await Assert.That(terminal).IsNull();
    }

    /// <summary>Verifies complete builders retain every element and stop at the materialization return.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task CompleteBuilderSequenceRetainsAllElementsAsync()
    {
        var block = (BlockSyntax)SyntaxFactory.ParseStatement("{ var b = F.CreateBuilder(); b.Add(1); b.Add(2); return b.ToArray(); Log(); }");
        var local = (LocalDeclarationStatementSyntax)block.Statements[0];
        await Assert.That(CollectionExpressionAdvancedAnalysis.TryGetBuilderSequence(local, out var elements, out var terminal)).IsTrue();
        await Assert.That(string.Join(",", elements.Select(static element => element.ToString()))).IsEqualTo("1,2");
        await Assert.That(terminal.ToString()).IsEqualTo("return b.ToArray();");
    }

    /// <summary>Checks configuration parameters cannot be mistaken for collection elements.</summary>
    /// <param name="target">The target collection type.</param>
    /// <param name="parameter">The factory parameter type.</param>
    /// <param name="expected">Whether all parameters carry elements.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("System.Collections.Generic.IEnumerable<int>", "int", true)]
    [Arguments("System.Collections.Generic.List<int>", "int[]", true)]
    [Arguments("System.Collections.Generic.List<int>", "System.Collections.Generic.IEnumerable<int>", true)]
    [Arguments("System.Collections.Generic.List<int>", "System.ReadOnlySpan<int>", true)]
    [Arguments("System.Collections.Generic.List<int>", "System.Span<int>", true)]
    [Arguments("System.Collections.Generic.List<int>", "System.Collections.Immutable.ImmutableArray<int>", true)]
    [Arguments("System.Collections.Generic.List<int>", "System.Collections.Generic.IEqualityComparer<int>", false)]
    [Arguments("System.Collections.Generic.List<int>", "string[]", false)]
    [Arguments("System.Collections.Generic.List<int>", "int[,]", false)]
    [Arguments("System.Collections.Generic.List<int>", "System.Collections.Generic.IEnumerable<string>", false)]
    [Arguments("System.Collections.Generic.List<int>", "string", false)]
    [Arguments("object", "int", false)]
    [Arguments("int[]", "int", false)]
    public async Task FactoryRequiresElementParametersAsync(string target, string parameter, bool expected)
    {
        var compilation = Compile($"class Factory {{ public static {target} Create({parameter} value) => default; }}");
        var method = compilation.GetTypeByMetadataName("Factory")!.GetMembers("Create").OfType<IMethodSymbol>().Single();
        await Assert.That(CollectionExpressionAdvancedAnalysis.FactoryTakesOnlyElements(method.ReturnType, method)).IsEqualTo(expected);
        await Assert.That(CollectionExpressionAdvancedAnalysis.FactoryTakesOnlyElements(null, method)).IsFalse();
    }

    /// <summary>Checks builder attributes must name the actual static factory owner and method.</summary>
    /// <param name="attribute">The target's attributes.</param>
    /// <param name="modifier">The factory modifier.</param>
    /// <param name="expected">Whether the factory is the declared builder.</param>
    /// <param name="targetSupported">Whether the target has a builder attribute.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("[System.Obsolete]", "static", false, false)]
    [Arguments("[System.Obsolete, System.Runtime.CompilerServices.CollectionBuilder(typeof(Factory), \"Create\")]", "static", true, true)]
    [Arguments("[System.Runtime.CompilerServices.CollectionBuilder(typeof(Other), \"Create\")]", "static", false, true)]
    [Arguments("[System.Runtime.CompilerServices.CollectionBuilder(typeof(Factory), \"Other\")]", "static", false, true)]
    [Arguments("[System.Runtime.CompilerServices.CollectionBuilder(null, \"Create\")]", "static", false, true)]
    [Arguments("[System.Runtime.CompilerServices.CollectionBuilder(typeof(Factory), null)]", "static", false, true)]
    [Arguments("[System.Runtime.CompilerServices.CollectionBuilder(typeof(Factory), \"Create\")]", "", false, true)]
    [Arguments("", "static", false, false)]
    public async Task BuilderAttributeMustIdentifyTheStaticFactoryAsync(string attribute, string modifier, bool expected, bool targetSupported)
    {
        const int RectangularRank = 2;
        var compilation = Compile($"{attribute} class Target {{ }} class Other {{ }} class Factory {{ public {modifier} Target Create() => null; }}");
        var target = compilation.GetTypeByMetadataName("Target")!;
        var method = compilation.GetTypeByMetadataName("Factory")!.GetMembers("Create").OfType<IMethodSymbol>().Single();
        await Assert.That(CollectionExpressionAdvancedAnalysis.TargetUsesBuilderMethod(target, method)).IsEqualTo(expected);
        await Assert.That(CollectionExpressionAdvancedAnalysis.TargetUsesBuilderMethod(null, method)).IsFalse();
        await Assert.That(CollectionExpressionAdvancedAnalysis.IsCollectionExpressionTarget(target)).IsEqualTo(targetSupported);
        await Assert.That(CollectionExpressionAdvancedAnalysis.IsCollectionExpressionTarget(null)).IsFalse();
        await Assert.That(CollectionExpressionAdvancedAnalysis.IsCollectionExpressionTarget(compilation.CreateArrayTypeSymbol(target))).IsTrue();
        await Assert.That(CollectionExpressionAdvancedAnalysis.IsCollectionExpressionTarget(compilation.CreateArrayTypeSymbol(target, RectangularRank))).IsFalse();
        await Assert.That(CollectionExpressionAdvancedAnalysis.HasCollectionBuilderAttribute(compilation)).IsTrue();
        await Assert.That(CollectionExpressionAdvancedAnalysis.HasCollectionBuilderAttribute(CSharpCompilation.Create("Empty"))).IsFalse();
    }

    /// <summary>Checks materialization requires a recognized extension on System.Linq.Enumerable.</summary>
    /// <param name="container">The extension namespace and class.</param>
    /// <param name="methodName">The extension method name.</param>
    /// <param name="receiver">The first parameter modifier.</param>
    /// <param name="expected">Whether the symbol is a LINQ materializer.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("namespace System.Linq { static class Enumerable", "ToArray", "this", true)]
    [Arguments("namespace System.Linq { static class Enumerable", "ToList", "this", true)]
    [Arguments("namespace System.Linq { static class Enumerable", "Other", "this", false)]
    [Arguments("namespace System.Linq { static class Enumerable", "ToArray", "", false)]
    [Arguments("namespace Other { static class Enumerable", "ToArray", "this", false)]
    [Arguments("namespace System.Linq { static class Other", "ToArray", "this", false)]
    public async Task LinqMaterializationChecksOwnerAndExtensionShapeAsync(string container, string methodName, string receiver, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"{container} {{ public static int[] {methodName}({receiver} int[] value) => value; }} }}");
        var compilation = CSharpCompilation.Create("Materialization", [tree], RuntimeMetadataReferences.Platform);
        var root = await tree.GetRootAsync();
        var declaration = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var method = compilation.GetSemanticModel(tree).GetDeclaredSymbol(declaration)!;
        await Assert.That(CollectionExpressionAdvancedAnalysis.IsLinqMaterialization(method)).IsEqualTo(expected);
    }

    /// <summary>Compiles source using shared runtime references.</summary>
    /// <param name="source">The compilation source.</param>
    /// <returns>The bound compilation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CSharpCompilation Compile(string source) => CSharpCompilation.Create("CollectionFactories", [CSharpSyntaxTree.ParseText(source)], RuntimeMetadataReferences.Platform);
}
