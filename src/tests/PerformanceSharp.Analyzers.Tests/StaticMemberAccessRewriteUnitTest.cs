// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Direct tests for <see cref="StaticMemberAccessRewrite"/>, shared by the PSH1022 and PSH1412 code fixes.</summary>
public class StaticMemberAccessRewriteUnitTest
{
    /// <summary>The static member the tests read.</summary>
    private const string MemberName = "Shared";

    /// <summary>Verifies the written type name is reused, in expression form, and the creation's trivia carries over.</summary>
    /// <param name="creationSource">The creation expression source.</param>
    /// <param name="expected">The member read the creation becomes.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("/*a*/new Random()/*b*/", "/*a*/Random.Shared/*b*/")]
    [Arguments("new System.Random()", "System.Random.Shared")]
    [Arguments("new global::System.Random()", "global::System.Random.Shared")]
    [Arguments("new()", "Random.Shared")]
    public async Task BuildsTheStaticMemberReadAsync(string creationSource, string expected)
    {
        var creation = (BaseObjectCreationExpressionSyntax)SyntaxFactory.ParseExpression(creationSource);

        var access = StaticMemberAccessRewrite.FromCreation(creation, nameof(Random), MemberName);

        await Assert.That(access.ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies a qualified type name becomes member accesses rather than a qualified name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedTypeBecomesMemberAccessAsync()
    {
        var creation = (BaseObjectCreationExpressionSyntax)SyntaxFactory.ParseExpression("new System.Random()");

        var access = StaticMemberAccessRewrite.FromCreation(creation, nameof(Random), MemberName);

        await Assert.That(access.Expression).IsTypeOf<MemberAccessExpressionSyntax>();
    }
}
