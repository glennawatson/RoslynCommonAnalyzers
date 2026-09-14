// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the syntax gate and binding check for calls on <c>System.GC</c>.</summary>
public class GcInvocationTests
{
    /// <summary>Verifies the receiver is accepted when its written name ends in <c>GC</c>.</summary>
    /// <param name="call">A member call.</param>
    /// <param name="expected">Whether the receiver is written as <c>GC</c>.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("GC.Collect()", true)]
    [Arguments("System.GC.Collect()", true)]
    [Arguments("global::System.GC.Collect()", true)]
    [Arguments("global::GC.Collect()", true)]
    [Arguments("Gc.Collect()", false)]
    [Arguments("this.Collect()", false)]
    [Arguments("Load().Collect()", false)]
    public async Task ReceiverMustBeWrittenAsGcAsync(string call, bool expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(call);
        var receiver = ((MemberAccessExpressionSyntax)invocation.Expression).Expression;

        await Assert.That(GcInvocation.IsGcReceiver(receiver)).IsEqualTo(expected);
    }

    /// <summary>Verifies only a method declared on the resolved <c>System.GC</c> binds.</summary>
    /// <param name="call">A statement-level call.</param>
    /// <param name="expected">Whether the call binds to a <c>System.GC</c> method.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("System.GC.Collect()", true)]
    [Arguments("System.GC.SuppressFinalize(this)", true)]
    [Arguments("GC.Collect()", false)]
    [Arguments("Unknown.Collect()", false)]
    public async Task CallMustBindToTheResolvedGcTypeAsync(string call, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {call}; }} }} static class GC {{ public static void Collect() {{ }} }}");
        var compilation = CSharpCompilation.Create(nameof(CallMustBindToTheResolvedGcTypeAsync), [tree], RuntimeMetadataReferences.Platform);
        var invocation = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var gcType = compilation.GetTypeByMetadataName("System.GC")!;

        await Assert.That(GcInvocation.BindsToGcMethod(compilation.GetSemanticModel(tree), invocation, gcType, CancellationToken.None)).IsEqualTo(expected);
    }
}
