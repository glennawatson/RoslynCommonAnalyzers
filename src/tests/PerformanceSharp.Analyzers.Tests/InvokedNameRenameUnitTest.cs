// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Direct tests for <see cref="InvokedNameRename"/>, shared by the PSH1212 and PSH1218 code fixes.</summary>
public class InvokedNameRenameUnitTest
{
    /// <summary>Verifies the invoked member's name is replaced and keeps its trivia.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RenamesTheInvokedMemberAsync()
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression("text. Substring /*c*/(1)");

        var edit = InvokedNameRename.Replace(invocation, "AsSpan");

        await Assert.That(edit.Original).IsSameReferenceAs(((MemberAccessExpressionSyntax)invocation.Expression).Name);
        await Assert.That(edit.Replacement.ToFullString()).IsEqualTo("AsSpan /*c*/");
    }
}
