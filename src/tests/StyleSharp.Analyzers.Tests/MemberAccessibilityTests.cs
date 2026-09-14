// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the reading of a member's written accessibility.</summary>
public class MemberAccessibilityTests
{
    /// <summary>The source every member test binds against.</summary>
    private const string Source = """
        public class Owner
        {
            public int Field;
            public int Property { get; set; }
            public event System.Action Changed;
            public void Method() { }
            public override string ToString() => "owner";
            public static implicit operator int(Owner owner) => 0;
            public class Nested { }
        }
        """;

    /// <summary>Verifies ordinary members count while overrides, operators and synthesized members do not.</summary>
    /// <param name="memberName">The member's metadata name.</param>
    /// <param name="expected">Whether the member's accessibility is authored.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Field", true)]
    [Arguments("Property", true)]
    [Arguments("Changed", true)]
    [Arguments("Method", true)]
    [Arguments("Nested", true)]
    [Arguments("ToString", false)]
    [Arguments("op_Implicit", false)]
    [Arguments("get_Property", false)]
    [Arguments(".ctor", false)]
    public async Task OrdinaryMembersAreAuthoredAsync(string memberName, bool expected)
    {
        var owner = SemanticModelFactory.Create(Source).Model.Compilation.GetTypeByMetadataName("Owner")!;
        var member = owner.GetMembers(memberName)[0];

        await Assert.That(MemberAccessibility.IsAuthored(member)).IsEqualTo(expected);
    }

    /// <summary>Verifies each accessibility is spelled the way C# writes it.</summary>
    /// <param name="accessibility">The accessibility.</param>
    /// <param name="expected">The keyword text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(Accessibility.Public, "public")]
    [Arguments(Accessibility.ProtectedOrInternal, "protected internal")]
    [Arguments(Accessibility.Protected, "protected")]
    [Arguments(Accessibility.Internal, "internal")]
    [Arguments(Accessibility.ProtectedAndInternal, "private protected")]
    [Arguments(Accessibility.Private, "private")]
    [Arguments(Accessibility.NotApplicable, "NotApplicable")]
    public async Task KeywordSpellsTheAccessibilityAsync(Accessibility accessibility, string expected)
    {
        var keyword = MemberAccessibility.Keyword(accessibility);

        await Assert.That(keyword).IsEqualTo(expected);
    }
}
