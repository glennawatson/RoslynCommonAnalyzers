// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests whether a method overrides a member that <c>object</c> declares.</summary>
public class ObjectMemberOverridesTests
{
    /// <summary>The source every override test binds against.</summary>
    private const string Source = """
        class Base
        {
            public override string ToString() => "base";
            public virtual int Measure() => 0;
        }

        class Derived : Base
        {
            public override string ToString() => "derived";
            public override int Measure() => 1;
            public int Plain() => 2;
        }
        """;

    /// <summary>Verifies an override chain rooted at <c>object</c> counts at any depth, and other methods do not.</summary>
    /// <param name="typeName">The declaring type.</param>
    /// <param name="methodName">The method.</param>
    /// <param name="expected">Whether the method overrides an <c>object</c> member.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Base", "ToString", true)]
    [Arguments("Derived", "ToString", true)]
    [Arguments("Derived", "Measure", false)]
    [Arguments("Derived", "Plain", false)]
    [Arguments("Base", "Measure", false)]
    public async Task OverrideChainRootedAtObjectCountsAsync(string typeName, string methodName, bool expected)
    {
        var compilation = SemanticModelFactory.Create(Source).Model.Compilation;
        var method = (IMethodSymbol)compilation.GetTypeByMetadataName(typeName)!.GetMembers(methodName)[0];

        await Assert.That(ObjectMemberOverrides.IsOverrideOfObjectMember(method)).IsEqualTo(expected);
    }
}
