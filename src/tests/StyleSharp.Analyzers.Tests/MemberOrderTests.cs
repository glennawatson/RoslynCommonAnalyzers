// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests member rank classification and lexicographic precedence.</summary>
public class MemberOrderTests
{
    /// <summary>Checks each declaration's kind rank and representative token.</summary>
    /// <param name="source">The member declaration.</param>
    /// <param name="kind">The expected kind rank.</param>
    /// <param name="name">The diagnostic token.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("int field;", 0, "field")]
    [Arguments("C() {}", 1, "C")]
    [Arguments("~C() {}", 2, "C")]
    [Arguments("delegate void D();", 3, "D")]
    [Arguments("event System.Action E;", 4, "E")]
    [Arguments("event System.Action E { add {} remove {} }", 4, "E")]
    [Arguments("enum E {}", 5, "E")]
    [Arguments("interface I {}", 6, "I")]
    [Arguments("int P { get; }", 7, "P")]
    [Arguments("int this[int i] => i;", 8, "this")]
    [Arguments("void M() {}", 9, "M")]
    [Arguments("public static C operator +(C a, C b) => a;", 9, "+")]
    [Arguments("public static implicit operator int(C a) => 0;", 9, "operator")]
    [Arguments("struct S {}", 10, "S")]
    [Arguments("record struct S;", 10, "S")]
    [Arguments("class N {}", 11, "N")]
    [Arguments("record N;", 12, "N")]
    public async Task ClassifyAndNameTokenRecognizeMemberKindsAsync(string source, int kind, string name)
    {
        const int UnionKind = 13;
        var member = SyntaxFactory.ParseMemberDeclaration(source)!;
        await Assert.That(MemberOrder.Classify(member)?.Kind).IsEqualTo(kind);
        await Assert.That(MemberOrder.NameToken(member).ValueText).IsEqualTo(name);
        await Assert.That(MemberOrder.Classify(member, isUnion: true)?.Kind).IsEqualTo(UnionKind);
    }

    /// <summary>Checks access modifiers, interface defaults, constants, and field-only readonly ranking.</summary>
    /// <param name="source">The containing type.</param>
    /// <param name="access">The access rank.</param>
    /// <param name="constant">The constant rank.</param>
    /// <param name="staticRank">The static rank.</param>
    /// <param name="readOnly">The readonly rank.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("class C { public const int X = 1; }", 0, 0, 0, 1)]
    [Arguments("class C { internal static readonly int X; }", 1, 1, 0, 0)]
    [Arguments("class C { protected internal int X; }", 2, 1, 1, 1)]
    [Arguments("class C { protected int X; }", 3, 1, 1, 1)]
    [Arguments("class C { private protected int X; }", 4, 1, 1, 1)]
    [Arguments("class C { private int X; }", 5, 1, 1, 1)]
    [Arguments("class C { volatile int X; }", 5, 1, 1, 1)]
    [Arguments("interface C { int M(); }", 0, 1, 1, 1)]
    [Arguments("interface C { private int M() => 0; }", 5, 1, 1, 1)]
    [Arguments("class C { static C() {} }", 0, 1, 0, 1)]
    [Arguments("struct C { public readonly int M() => 0; }", 0, 1, 1, 1)]
    public async Task ClassifyUsesModifierAndContainerFactsAsync(string source, int access, int constant, int staticRank, int readOnly)
    {
        var type = (TypeDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(source)!;
        var order = MemberOrder.Classify(type.Members[0])!.Value;
        await Assert.That((order.Access, order.Constant, order.Static, order.ReadOnly)).IsEqualTo((access, constant, staticRank, readOnly));
    }

    /// <summary>Checks explicit interface implementations and unsupported members are skipped.</summary>
    /// <param name="source">The declaration.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("void I.M() {}")]
    [Arguments("int I.P => 0;")]
    [Arguments("int I.this[int i] => i;")]
    [Arguments("event System.Action I.E { add {} remove {} }")]
    public async Task ClassifySkipsExplicitAndUnsupportedMembersAsync(string source)
    {
        var member = SyntaxFactory.ParseMemberDeclaration(source)!;
        await Assert.That(MemberOrder.Classify(member)).IsNull();
    }

    /// <summary>Checks fallback tokens on syntax that lacks a declared variable or ordinary member name.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task NameTokenFallsBackForIncompleteDeclarationsAsync()
    {
        var declaration = SyntaxFactory.VariableDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)));
        var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName("N"));
        await Assert.That(MemberOrder.NameToken(SyntaxFactory.FieldDeclaration(declaration)).ValueText).IsEqualTo("int");
        await Assert.That(MemberOrder.NameToken(namespaceDeclaration).ValueText).IsEqualTo("namespace");
        await Assert.That(MemberOrder.Classify(namespaceDeclaration)).IsNull();
        await Assert.That(MemberOrder.Classify(SyntaxFactory.IncompleteMember())).IsNull();
    }

    /// <summary>Checks each dimension decides ordering before later dimensions and selects the matching rule.</summary>
    /// <param name="dimension">The dimension changed from its baseline.</param>
    /// <param name="rule">The corresponding diagnostic.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(0, "SST1201")]
    [Arguments(1, "SST1202")]
    [Arguments(2, "SST1203")]
    [Arguments(3, "SST1204")]
    [Arguments(4, "SST1215")]
    public async Task CompareAndViolationUseFirstDifferentDimensionAsync(int dimension, string rule)
    {
        var previous = new MemberOrder(1, 1, 1, 1, 1);
        var values = new[] { 1, 1, 1, 1, 1 };
        values[dimension] = 0;
        var current = new MemberOrder(values[0], values[1], values[2], values[3], values[4]);
        await Assert.That(current.CompareTo(previous)).IsEqualTo(-1);
        await Assert.That(previous.CompareTo(current)).IsEqualTo(1);
        await Assert.That(current.CompareTo(current)).IsEqualTo(0);
        await Assert.That(current.ViolationAfter(previous)?.Id).IsEqualTo(rule);
        await Assert.That(previous.ViolationAfter(current)).IsNull();
        await Assert.That(current.ViolationAfter(current)).IsNull();
        var staticReadonly = new MemberOrder(0, 0, 1, 0, 0);
        await Assert.That(staticReadonly.ViolationAfter(staticReadonly with { ReadOnly = 1 })?.Id).IsEqualTo("SST1214");
    }

    /// <summary>Checks union detection on classes, records, inherited markers, and value types.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task IsUnionRequiresReferenceTypeImplementingTheMarkerAsync()
    {
        const string Source = """
            namespace System.Runtime.CompilerServices { interface IUnion {} }
            class A : System.IDisposable { public void Dispose() {} }
            class B : A, System.Runtime.CompilerServices.IUnion {}
            record R : System.Runtime.CompilerServices.IUnion;
            record struct S : System.Runtime.CompilerServices.IUnion;
            struct V {} class Plain {}
            """;
        var tree = CSharpSyntaxTree.ParseText(Source);
        var compilation = CSharpCompilation.Create("Unions", [tree], RuntimeMetadataReferences.Platform);
        var marker = MemberOrder.ResolveUnionMarker(compilation)!;
        var model = compilation.GetSemanticModel(tree);
        var root = (CompilationUnitSyntax)await tree.GetRootAsync();
        foreach (var member in root.Members.OfType<TypeDeclarationSyntax>())
        {
            await Assert.That(MemberOrder.IsUnion(member, model, marker, CancellationToken.None)).IsEqualTo(member.Identifier.ValueText is "B" or "R");
        }

        await Assert.That(MemberOrder.ResolveUnionMarker(CSharpCompilation.Create("Empty"))).IsNull();
    }
}
