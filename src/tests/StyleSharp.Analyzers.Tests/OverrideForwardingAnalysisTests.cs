// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the syntactic contract for redundant override forwarding.</summary>
public class OverrideForwardingAnalysisTests
{
    /// <summary>Verifies only an unchanged base call with matching parameter passing is redundant.</summary>
    /// <param name="source">The method declaration.</param>
    /// <param name="expected">Whether the method forwards unchanged.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public override int M(int x) => base.M(x);", true)]
    [Arguments("public override int M(int x) { return base.M(x); }", true)]
    [Arguments("public override void M() { base.M(); }", true)]
    [Arguments("public override void M(ref int x) => base.M(ref x);", true)]
    [Arguments("public override void M(out int x) => base.M(out x);", true)]
    [Arguments("public override void M(in int x) => base.M(in x);", true)]
    [Arguments("public override void M(params int[] x) => base.M(x);", true)]
    [Arguments("public override void M(scoped ref int x) => base.M(ref x);", true)]
    [Arguments("public int M(int x) => base.M(x);", false)]
    [Arguments("[A] public override int M(int x) => base.M(x);", false)]
    [Arguments("public sealed override int M(int x) => base.M(x);", false)]
    [Arguments("public override T M<T>(T x) => base.M(x);", false)]
    [Arguments("public override int M(int x) => x;", false)]
    [Arguments("public abstract override int M(int x);", false)]
    [Arguments("public override void M() { }", false)]
    [Arguments("public override void M() { base.M(); base.M(); }", false)]
    [Arguments("public override void M() { throw null; }", false)]
    [Arguments("public override int M() { return 1; }", false)]
    [Arguments("public override void M() { x = 1; }", false)]
    [Arguments("public override int M(int x) => M(x);", false)]
    [Arguments("public override int M(int x) => this.M(x);", false)]
    [Arguments("public override int M(int x) => base.N(x);", false)]
    [Arguments("public override int M(int x) => base.M<int>(x);", false)]
    [Arguments("public override int M(int x) => base.M();", false)]
    [Arguments("public override int M(int x) => base.M(1);", false)]
    [Arguments("public override int M(int x, int y) => base.M(y, x);", false)]
    [Arguments("public override void M(ref int x) => base.M(x);", false)]
    [Arguments("public override void M(int x) => base.M(ref x);", false)]
    public async Task MethodMustForwardParametersUnchangedAsync(string source, bool expected)
    {
        var method = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(source)!;
        await Assert.That(OverrideForwardingAnalysis.IsPlainForwardingMethod(method)).IsEqualTo(expected);
    }

    /// <summary>Verifies every property accessor must forward the corresponding base accessor.</summary>
    /// <param name="source">The property declaration.</param>
    /// <param name="expected">Whether all accessors forward unchanged.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public override int P => base.P;", true)]
    [Arguments("public override int P { get => base.P; set => base.P = value; }", true)]
    [Arguments("public override int P { get { return base.P; } set { base.P = value; } }", true)]
    [Arguments("public override int P { get => base.P; init => base.P = value; }", true)]
    [Arguments("public int P => base.P;", false)]
    [Arguments("public override int P => this.P;", false)]
    [Arguments("public override int P => base.Q;", false)]
    [Arguments("public override int P { }", false)]
    [Arguments("public override int P { get; set; }", false)]
    [Arguments("public override int P { get { } }", false)]
    [Arguments("public override int P { get { M(); return base.P; } }", false)]
    [Arguments("public override int P { get { throw null; } }", false)]
    [Arguments("public override int P { get { return; } }", false)]
    [Arguments("public override int P { get => base.P; set => M(); }", false)]
    [Arguments("public override int P { set => base.P += value; }", false)]
    [Arguments("public override int P { set => base.P = 1; }", false)]
    [Arguments("public override int P { set => base.P = other; }", false)]
    [Arguments("public override int P { set => this.P = value; }", false)]
    [Arguments("public override int P { add => base.P; }", false)]
    public async Task PropertyMustForwardEveryAccessorAsync(string source, bool expected)
    {
        var property = (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(source)!;
        await Assert.That(OverrideForwardingAnalysis.IsPlainForwardingProperty(property)).IsEqualTo(expected);
    }

    /// <summary>Verifies a property without either accessor or expression syntax cannot forward.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PropertyWithoutBodyCannotForwardAsync()
    {
        var property = ((PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("public override int P { get; }")!).WithAccessorList(null);
        await Assert.That(OverrideForwardingAnalysis.IsPlainForwardingProperty(property)).IsFalse();
    }
}
