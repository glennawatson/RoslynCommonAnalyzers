// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Direct tests for <see cref="EnumerableInvocationHelper"/>, the <c>System.Linq.Enumerable</c> resolution shared by the LINQ chain, usage, and native-method analyzers.</summary>
public class EnumerableInvocationHelperUnitTest
{
    /// <summary>A compilation unit exercising an Enumerable call, a native call, and a string call.</summary>
    private const string Source =
        """
        using System.Collections.Generic;
        using System.Linq;

        class C
        {
            void M(List<int> xs)
            {
                var a = xs.Where(x => x > 0);
                xs.Add(1);
                var s = "hi".Substring(0);
            }
        }
        """;

    /// <summary>The unreduced arity of a LINQ operator taking its source and one more argument.</summary>
    private const int SourceAndArgumentArity = 2;

    /// <summary>Verifies only extension-form calls declared on the resolved type match, and the arity overloads filter on the unreduced parameter count.</summary>
    /// <param name="call">A call on the <c>list</c> parameter.</param>
    /// <param name="reduced">Whether the call is a reduced extension declared on <c>Enumerable</c>.</param>
    /// <param name="sourceOnly">Whether that extension takes only its source.</param>
    /// <param name="twoParameters">Whether that extension takes its source and one more argument.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("list.First()", true, true, false)]
    [Arguments("list.ElementAt(0)", true, false, true)]
    [Arguments("list.First(x => x > 0)", true, false, true)]
    [Arguments("Enumerable.First(list)", false, false, false)]
    [Arguments("list.Contains(1)", false, false, false)]
    [Arguments("list.Largest()", false, false, false)]
    [Arguments("list.Missing()", false, false, false)]
    public async Task ReducedExtensionMustBeDeclaredOnTheResolvedTypeAsync(string call, bool reduced, bool sourceOnly, bool twoParameters)
    {
        var compilation = Compile(
            $$"""
            using System.Collections.Generic;
            using System.Linq;
            static class Extensions { public static int Largest(this IEnumerable<int> source) => 0; }
            class C { object M(List<int> list) => {{call}}; }
            """);
        var tree = compilation.SyntaxTrees[0];
        var model = compilation.GetSemanticModel(tree);
        var invocation = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var enumerable = compilation.GetTypeByMetadataName("System.Linq.Enumerable")!;

        await Assert.That(EnumerableInvocationHelper.IsReducedExtensionOn(model, invocation, enumerable, CancellationToken.None)).IsEqualTo(reduced);
        await Assert.That(EnumerableInvocationHelper.IsSourceOnlyExtensionOn(model, invocation, enumerable, CancellationToken.None)).IsEqualTo(sourceOnly);
        await Assert.That(EnumerableInvocationHelper.IsReducedExtensionOn(model, invocation, enumerable, SourceAndArgumentArity, CancellationToken.None)).IsEqualTo(twoParameters);
    }

    /// <summary>Verifies the Enumerable type is recognized and other types are not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsSystemLinqEnumerableRecognizesOnlyTheEnumerableTypeAsync()
    {
        var compilation = Compile(Source);

        await Assert.That(EnumerableInvocationHelper.IsSystemLinqEnumerable(compilation.GetTypeByMetadataName("System.Linq.Enumerable"))).IsTrue();
        await Assert.That(EnumerableInvocationHelper.IsSystemLinqEnumerable(compilation.GetSpecialType(SpecialType.System_String))).IsFalse();
        await Assert.That(EnumerableInvocationHelper.IsSystemLinqEnumerable(null)).IsFalse();
    }

    /// <summary>Verifies a reduced Enumerable extension call is resolved to its bound method.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetEnumerableMethodResolvesAReducedLinqCallAsync()
    {
        var (model, root) = Bind(Source);
        var where = InvocationNamed(root, "Where");

        await Assert.That(EnumerableInvocationHelper.TryGetEnumerableMethod(where, model, CancellationToken.None, out var method)).IsTrue();
        await Assert.That(method.Name).IsEqualTo("Where");
        await Assert.That(EnumerableInvocationHelper.IsEnumerableInvocation(where, model, CancellationToken.None)).IsTrue();
    }

    /// <summary>Verifies a native collection call and a string call are both rejected.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetEnumerableMethodRejectsNonEnumerableCallsAsync()
    {
        var (model, root) = Bind(Source);

        await Assert.That(EnumerableInvocationHelper.IsEnumerableInvocation(InvocationNamed(root, "Add"), model, CancellationToken.None)).IsFalse();
        await Assert.That(EnumerableInvocationHelper.IsEnumerableInvocation(InvocationNamed(root, "Substring"), model, CancellationToken.None)).IsFalse();
    }

    /// <summary>Compiles the source and returns its semantic model and syntax root.</summary>
    /// <param name="source">The compilation unit source.</param>
    /// <returns>The semantic model and syntax root.</returns>
    private static (SemanticModel Model, SyntaxNode Root) Bind(string source)
    {
        var compilation = Compile(source);
        var tree = compilation.SyntaxTrees[0];
        return (compilation.GetSemanticModel(tree), tree.GetRoot());
    }

    /// <summary>Finds the first invocation whose member name matches.</summary>
    /// <param name="root">The syntax root to search.</param>
    /// <param name="memberName">The invoked member name.</param>
    /// <returns>The matching invocation.</returns>
    /// <exception cref="InvalidOperationException">No member access named <paramref name="memberName"/> is invoked anywhere under <paramref name="root"/>.</exception>
    private static InvocationExpressionSyntax InvocationNamed(SyntaxNode root, string memberName)
    {
        foreach (var node in root.DescendantNodes())
        {
            if (node is InvocationExpressionSyntax invocation
                && invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: { } text }
                && text == memberName)
            {
                return invocation;
            }
        }

        throw new InvalidOperationException($"No invocation of '{memberName}' was found.");
    }

    /// <summary>Compiles the source against the running framework's reference assemblies.</summary>
    /// <param name="source">The compilation unit source.</param>
    /// <returns>The compilation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CSharpCompilation Compile(string source) =>
        CSharpCompilation.Create(
            "EnumerableInvocationHelperTests",
            [CSharpSyntaxTree.ParseText(source)],
            RuntimeMetadataReferences.Platform,
            new(OutputKind.DynamicallyLinkedLibrary));
}
