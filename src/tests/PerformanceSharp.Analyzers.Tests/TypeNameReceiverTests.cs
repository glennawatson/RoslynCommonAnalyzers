// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the syntax gates for a static call written against a type name.</summary>
public class TypeNameReceiverTests
{
    /// <summary>The type name every receiver is matched against.</summary>
    private const string TypeName = "Thread";

    /// <summary>Verifies the receiver matches when its rightmost identifier is the type name.</summary>
    /// <param name="receiver">The receiver expression.</param>
    /// <param name="expected">Whether the receiver names the type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Thread", true)]
    [Arguments("System.Threading.Thread", true)]
    [Arguments("global::System.Threading.Thread", true)]
    [Arguments("global::Thread", false)]
    [Arguments("Thread.CurrentThread", false)]
    [Arguments("thread", false)]
    [Arguments("this", false)]
    [Arguments("Load()", false)]
    public async Task ReceiverEndsWithTheTypeNameAsync(string receiver, bool expected) =>
        await Assert.That(TypeNameReceiver.EndsWithTypeName(SyntaxFactory.ParseExpression(receiver), TypeName)).IsEqualTo(expected);

    /// <summary>Verifies the call matches only the named member on a receiver written as the type name.</summary>
    /// <param name="call">The invocation text.</param>
    /// <param name="expected">Whether the invocation is the named call on the type name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Thread.Sleep(1)", true)]
    [Arguments("System.Threading.Thread.Sleep(1)", true)]
    [Arguments("Thread.Sleep()", true)]
    [Arguments("Thread.Yield()", false)]
    [Arguments("thread.Sleep(1)", false)]
    [Arguments("Sleep(1)", false)]
    public async Task CallNamesTheMemberOnTheTypeNameAsync(string call, bool expected) =>
        await Assert.That(TypeNameReceiver.IsCallOnTypeName((InvocationExpressionSyntax)SyntaxFactory.ParseExpression(call), "Sleep", TypeName)).IsEqualTo(expected);
}
