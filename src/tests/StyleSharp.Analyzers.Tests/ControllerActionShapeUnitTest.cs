// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the routable-action shape shared by the MVC controller rules.</summary>
public sealed class ControllerActionShapeUnitTest
{
    /// <summary>The source whose controller methods the tests read.</summary>
    private const string Source =
        """
        abstract class Controller
        {
            public void Action() { }
            public static void StaticMethod() { }
            public abstract void AbstractMethod();
            public void GenericMethod<T>() { }
            private void PrivateMethod() { }
            public override string ToString() => string.Empty;
            public int Property { get; set; }
        }
        """;

    /// <summary>The controller type every test reads.</summary>
    private static readonly INamedTypeSymbol ControllerType = CSharpCompilation.Create(
            nameof(ControllerActionShapeUnitTest),
            [CSharpSyntaxTree.ParseText(Source)],
            RuntimeMetadataReferences.Platform)
        .GetTypeByMetadataName("Controller")!;

    /// <summary>Verifies only a public, instance, concrete, non-generic ordinary method that is not an object override is routable.</summary>
    /// <param name="name">The method name.</param>
    /// <param name="expected">Whether the method is routable.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("Action", true)]
    [Arguments("StaticMethod", false)]
    [Arguments("AbstractMethod", false)]
    [Arguments("GenericMethod", false)]
    [Arguments("PrivateMethod", false)]
    [Arguments("ToString", false)]
    [Arguments("get_Property", false)]
    public async Task IsRoutableAsync(string name, bool expected) =>
        await Assert.That(ControllerActionShape.IsRoutable((IMethodSymbol)ControllerType.GetMembers(name)[0])).IsEqualTo(expected);
}
