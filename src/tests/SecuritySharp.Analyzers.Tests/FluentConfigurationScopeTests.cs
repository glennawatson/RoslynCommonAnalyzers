// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Tests the local configuration scope around a fluent builder call.</summary>
public class FluentConfigurationScopeTests
{
    /// <summary>The builder shapes the scope search binds against.</summary>
    private const string BuilderSource =
        """
        class Builder { public Builder Allow() => this; public Builder Deny() => this; }
        class Other { public Other Allow() => this; }
        """;

    /// <summary>Verifies the nearest lambda body wins, and outside a lambda the nearest statement or clause is the scope.</summary>
    /// <param name="members">Class members containing one <c>Target()</c> member call.</param>
    /// <param name="expectedScope">The syntax kind of the expected scope, or <see cref="SyntaxKind.None"/> when there is none.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("void M(System.Action<B> f) { f(b => b.Deny().Target()); }", SyntaxKind.InvocationExpression)]
    [Arguments("void M(System.Action<B> f) { f(b => { b.Deny(); b.Target(); }); }", SyntaxKind.Block)]
    [Arguments("void M(B b) { b.Deny().Target(); }", SyntaxKind.ExpressionStatement)]
    [Arguments("void M(B b) { var x = b.Target(); }", SyntaxKind.EqualsValueClause)]
    [Arguments("object M(B b) => b.Target();", SyntaxKind.ArrowExpressionClause)]
    [Arguments("static object F = new B().Target();", SyntaxKind.EqualsValueClause)]
    [Arguments("void M(B b) { System.Action a = () => { void L() { b.Target(); } }; }", SyntaxKind.ExpressionStatement)]
    [Arguments("int P { get; } = X(b => b.Target());", SyntaxKind.InvocationExpression)]
    public async Task ScopePrefersTheLambdaBodyThenTheNearestLocalUnitAsync(string members, SyntaxKind expectedScope)
    {
        var target = SyntaxFactory.ParseCompilationUnit($"class C {{ {members} }}")
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Single(static invocation => FluentConfigurationScope.GetInvokedName(invocation.Expression)?.Identifier.ValueText == "Target");

        await Assert.That(FluentConfigurationScope.GetScope(target)?.Kind() ?? SyntaxKind.None).IsEqualTo(expectedScope);
    }

    /// <summary>Verifies a detached call has no scope.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedCallHasNoScopeAsync() =>
        await Assert.That(FluentConfigurationScope.GetScope(SyntaxFactory.ParseExpression("b.Target()"))).IsNull();

    /// <summary>Verifies only a member access or member binding names the invoked member.</summary>
    /// <param name="call">The invocation text.</param>
    /// <param name="expected">The invoked member name, or an empty string when there is none.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("a.Target()", "Target")]
    [Arguments("a.Target<int>()", "Target")]
    [Arguments("a?.Target()", "Target")]
    [Arguments("Target()", "")]
    public async Task InvokedNameNeedsAReceiverAsync(string call, string expected)
    {
        var invocation = SyntaxFactory.ParseExpression(call).DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Single();

        await Assert.That(FluentConfigurationScope.GetInvokedName(invocation.Expression)?.Identifier.ValueText ?? string.Empty).IsEqualTo(expected);
    }

    /// <summary>Verifies the scope search finds a matching call anywhere in the scope, the scope root included, and binds its type.</summary>
    /// <param name="method">A method whose body, or lambda body, is the scope.</param>
    /// <param name="expected">Whether a matching <c>Builder.Allow</c> call is present.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("void M(Builder b) { b.Deny().Allow(); }", true)]
    [Arguments("void M(Builder b) { b.Deny(); if (true) { b.Allow(); } }", true)]
    [Arguments("void M(System.Func<Builder, Builder> f) { M(b => b.Allow()); }", true)]
    [Arguments("void M(Builder b) { b.Deny(); }", false)]
    [Arguments("void M(Other o) { o.Allow(); }", false)]
    public async Task ScopeSearchBindsTheMatchingCallAsync(string method, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"{BuilderSource} class C {{ {method} }}");
        var compilation = CSharpCompilation.Create(nameof(ScopeSearchBindsTheMatchingCallAsync), [tree], [RuntimeMetadataReferences.CoreLibrary]);
        var root = await tree.GetRootAsync();
        var declaration = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(static m => m.Identifier.ValueText == "M");
        SyntaxNode scope = declaration.DescendantNodes().OfType<SimpleLambdaExpressionSyntax>().FirstOrDefault()?.Body ?? declaration.Body!;
        var builder = compilation.GetTypeByMetadataName("Builder")!;

        await Assert.That(FluentConfigurationScope.ContainsBuilderCall(scope, compilation.GetSemanticModel(tree), builder, IsAllowCall, CancellationToken.None))
            .IsEqualTo(expected);
    }

    /// <summary>Returns whether an invocation is a <c>Builder.Allow</c> call.</summary>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="builderType">The builder type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for an <c>Allow</c> call on the builder.</returns>
    private static bool IsAllowCall(InvocationExpressionSyntax invocation, SemanticModel model, INamedTypeSymbol builderType, CancellationToken cancellationToken) =>
        FluentConfigurationScope.GetInvokedName(invocation.Expression) is { Identifier.ValueText: "Allow" }
            && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, builderType);
}
