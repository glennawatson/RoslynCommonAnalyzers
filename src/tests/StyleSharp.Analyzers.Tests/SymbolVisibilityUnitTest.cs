// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared external-visibility check.</summary>
public sealed class SymbolVisibilityUnitTest
{
    /// <summary>The source whose members the tests inspect.</summary>
    private const string Source =
        """
        public class Outer
        {
            public void PublicMethod() { }
            private void PrivateMethod() { }
        }

        internal class Hidden
        {
            public void PublicMethodInInternalType() { }
        }
        """;

    /// <summary>Verifies a public member of a public type is externally visible.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicMemberOfPublicTypeIsExternallyVisibleAsync()
    {
        await Assert.That(SymbolVisibility.IsExternallyVisible(GetMethod("PublicMethod"))).IsTrue();
    }

    /// <summary>Verifies a private member is not externally visible.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateMemberIsNotExternallyVisibleAsync()
    {
        await Assert.That(SymbolVisibility.IsExternallyVisible(GetMethod("PrivateMethod"))).IsFalse();
    }

    /// <summary>Verifies a public member nested in an internal type is not externally visible.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicMemberOfInternalTypeIsNotExternallyVisibleAsync()
    {
        await Assert.That(SymbolVisibility.IsExternallyVisible(GetMethod("PublicMethodInInternalType"))).IsFalse();
    }

    /// <summary>Resolves the named method symbol from the shared source.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>The declared method symbol.</returns>
    private static ISymbol GetMethod(string name)
    {
        var (root, model) = SemanticModelFactory.Create(Source);
        foreach (var type in root.Members)
        {
            foreach (var member in ((TypeDeclarationSyntax)type).Members)
            {
                if (member is MethodDeclarationSyntax method && method.Identifier.ValueText == name)
                {
                    return model.GetDeclaredSymbol(method)!;
                }
            }
        }

        throw new InvalidOperationException($"Method '{name}' was not found.");
    }
}
