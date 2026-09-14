// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the binding check for a string builder's single-string instance overloads.</summary>
public class StringBuilderInvocationTests
{
    /// <summary>Verifies only an instance method of the builder taking exactly one string binds.</summary>
    /// <param name="call">A call on the <c>sb</c> parameter or another receiver.</param>
    /// <param name="expected">Whether the call binds to a single-string builder overload.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("sb.Append(\"a\")", true)]
    [Arguments("sb.AppendLine(\"a\")", true)]
    [Arguments("sb.Append(1)", false)]
    [Arguments("sb.Append('a')", false)]
    [Arguments("sb.Insert(0, \"a\")", false)]
    [Arguments("sb.AppendText(\"a\")", false)]
    [Arguments("Other.Append(\"a\")", false)]
    public async Task CallMustBindToAnInstanceStringOverloadOfTheBuilderAsync(string call, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText(
            $$"""
            using System.Text;
            class C { object M(StringBuilder sb) => {{call}}; }
            static class Other { public static object Append(string value) => value; }
            static class Extensions { public static StringBuilder AppendText(this StringBuilder sb, string value) => sb; }
            """);
        var compilation = CSharpCompilation.Create(nameof(CallMustBindToAnInstanceStringOverloadOfTheBuilderAsync), [tree], RuntimeMetadataReferences.Platform);
        var invocation = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var builderType = compilation.GetTypeByMetadataName("System.Text.StringBuilder")!;

        await Assert.That(StringBuilderInvocation.BindsToStringOverload(compilation.GetSemanticModel(tree), invocation, builderType, CancellationToken.None))
            .IsEqualTo(expected);
    }
}
