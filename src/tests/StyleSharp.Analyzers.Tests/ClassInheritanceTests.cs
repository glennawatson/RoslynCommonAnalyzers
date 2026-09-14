// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests whether a class has a base class other than <c>object</c>.</summary>
public class ClassInheritanceTests
{
    /// <summary>The source every inheritance test binds against.</summary>
    private const string Source = """
        class Root { }
        class Child : Root { }
        struct Value { }
        interface IShape { }
        static class Tools { }
        """;

    /// <summary>Verifies only a class deriving from another class qualifies.</summary>
    /// <param name="typeName">The type to test.</param>
    /// <param name="expected">Whether the type is a class with a non-object base.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Root", false)]
    [Arguments("Child", true)]
    [Arguments("Value", false)]
    [Arguments("IShape", false)]
    [Arguments("Tools", false)]
    public async Task OnlyAClassWithItsOwnBaseQualifiesAsync(string typeName, bool expected)
    {
        var type = SemanticModelFactory.Create(Source).Model.Compilation.GetTypeByMetadataName(typeName)!;

        await Assert.That(ClassInheritance.HasNonObjectBase(type)).IsEqualTo(expected);
    }
}
