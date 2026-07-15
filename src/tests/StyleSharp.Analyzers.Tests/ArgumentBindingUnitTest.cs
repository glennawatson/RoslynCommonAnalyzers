// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared argument-to-parameter binding helpers.</summary>
public sealed class ArgumentBindingUnitTest
{
    /// <summary>Verifies the argument list is pulled from calls, creations, and initializers, and nothing else.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetArgumentListRecognizesCallCreationAndInitializerAsync()
    {
        var (root, _) = SemanticModelFactory.Create(
            """
            class C : B
            {
                C() : base(1) { }
                void M()
                {
                    N(1);
                    _ = new C();
                }
            }
            class B { public B(int x) { } }
            """);
        var invocation = FirstDescendant<InvocationExpressionSyntax>(root);
        var creation = FirstDescendant<ObjectCreationExpressionSyntax>(root);
        var initializer = FirstDescendant<ConstructorInitializerSyntax>(root);
        var identifier = FirstDescendant<IdentifierNameSyntax>(root);

        await Assert.That(ArgumentBinding.GetArgumentList(invocation)).IsNotNull();
        await Assert.That(ArgumentBinding.GetArgumentList(creation)).IsNotNull();
        await Assert.That(ArgumentBinding.GetArgumentList(initializer)).IsNotNull();
        await Assert.That(ArgumentBinding.GetArgumentList(identifier)).IsNull();
    }

    /// <summary>Verifies optional-parameter detection distinguishes methods that have one from those that do not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasOptionalParameterReflectsTheMethodSignatureAsync()
    {
        var (root, model) = SemanticModelFactory.Create(
            """
            class C
            {
                void Optional(int a, int b = 0) { }
                void Required(int a) { }
            }
            """);

        await Assert.That(ArgumentBinding.HasOptionalParameter(GetMethod(root, model, "Optional"))).IsTrue();
        await Assert.That(ArgumentBinding.HasOptionalParameter(GetMethod(root, model, "Required"))).IsFalse();
    }

    /// <summary>Verifies positional arguments bind by index and named arguments bind by name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FindParameterMatchesPositionalByIndexAndNamedByNameAsync()
    {
        var (root, model) = SemanticModelFactory.Create(
            """
            class C
            {
                void M(int a, int b) { }
                void Caller() => M(1, b: 2);
            }
            """);
        var invocation = FirstDescendant<InvocationExpressionSyntax>(root);
        var method = (IMethodSymbol)model.GetSymbolInfo(invocation).Symbol!;
        var arguments = invocation.ArgumentList.Arguments;

        await Assert.That(ArgumentBinding.FindParameter(method, arguments, 0)!.Name).IsEqualTo("a");
        await Assert.That(ArgumentBinding.FindParameter(method, arguments, 1)!.Name).IsEqualTo("b");
    }

    /// <summary>Resolves the named method symbol.</summary>
    /// <param name="root">The compilation unit root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="name">The method name.</param>
    /// <returns>The declared method symbol.</returns>
    private static IMethodSymbol GetMethod(CompilationUnitSyntax root, SemanticModel model, string name)
    {
        foreach (var member in ((TypeDeclarationSyntax)root.Members[0]).Members)
        {
            if (member is MethodDeclarationSyntax method && method.Identifier.ValueText == name)
            {
                return (IMethodSymbol)model.GetDeclaredSymbol(method)!;
            }
        }

        throw new InvalidOperationException($"Method '{name}' was not found.");
    }

    /// <summary>Finds the first descendant node of the requested type.</summary>
    /// <typeparam name="T">The node type.</typeparam>
    /// <param name="root">The root to search.</param>
    /// <returns>The first matching descendant.</returns>
    private static T FirstDescendant<T>(SyntaxNode root)
        where T : SyntaxNode
    {
        foreach (var node in root.DescendantNodes())
        {
            if (node is T match)
            {
                return match;
            }
        }

        throw new InvalidOperationException($"No {typeof(T).Name} was found.");
    }
}
