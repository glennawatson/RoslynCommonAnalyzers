// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using TUnit.Assertions;

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MemberOrderingAnalyzer,
    StyleSharp.Analyzers.MemberOrderingCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the member-ordering rules (SST1201–SST1214) and their move fix.</summary>
public class MemberOrderingAnalyzerUnitTest
{
    /// <summary>Verifies members in the conventional order produce no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrderedNoDiagnosticAsync()
        => await Verify.VerifyAnalyzerAsync(
            "public class C\n{\n"
            + "    private int _field;\n"
            + "    public C() { }\n"
            + "    public int Property { get; set; }\n"
            + "    public void Method() { }\n"
            + "}");

    /// <summary>Verifies a field after a method is reported (SST1201) and moved before it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KindOutOfOrderAsync()
    {
        const string Source = "public class C\n{\n"
            + "    public void Method() { }\n"
            + "    private int {|SST1201:_field|};\n"
            + "}";
        const string FixedSource = "public class C\n{\n"
            + "    private int _field;\n"
            + "    public void Method() { }\n"
            + "}";

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a more accessible member after a less accessible one of the same kind is reported (SST1202) and moved up.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AccessOutOfOrderAsync()
    {
        const string Source = "public class C\n{\n"
            + "    private void A() { }\n"
            + "    public void {|SST1202:B|}() { }\n"
            + "}";
        const string FixedSource = "public class C\n{\n"
            + "    public void B() { }\n"
            + "    private void A() { }\n"
            + "}";

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a constant after a field of the same accessibility is reported (SST1203) and moved up.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantAfterFieldAsync()
    {
        const string Source = "public class C\n{\n"
            + "    private int _field;\n"
            + "    private const int {|SST1203:Max|} = 1;\n"
            + "}";
        const string FixedSource = "public class C\n{\n"
            + "    private const int Max = 1;\n"
            + "    private int _field;\n"
            + "}";

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a static member after an instance member of the same kind is reported (SST1204) and moved up.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticAfterInstanceAsync()
    {
        const string Source = "public class C\n{\n"
            + "    public void Instance() { }\n"
            + "    public static void {|SST1204:Shared|}() { }\n"
            + "}";
        const string FixedSource = "public class C\n{\n"
            + "    public static void Shared() { }\n"
            + "    public void Instance() { }\n"
            + "}";

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an instance readonly field after a non-readonly field of the same accessibility is reported (SST1215) and moved up.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceReadonlyAfterMutableAsync()
    {
        const string Source = "public class C\n{\n"
            + "    private int _mutable;\n"
            + "    private readonly int {|SST1215:_value|};\n"
            + "}";
        const string FixedSource = "public class C\n{\n"
            + "    private readonly int _value;\n"
            + "    private int _mutable;\n"
            + "}";

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a static readonly field after a static non-readonly field is reported (SST1214) and moved up.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticReadonlyAfterMutableAsync()
    {
        const string Source = "public class C\n{\n"
            + "    private static int _mutable;\n"
            + "    private static readonly int {|SST1214:_value|};\n"
            + "}";
        const string FixedSource = "public class C\n{\n"
            + "    private static readonly int _value;\n"
            + "    private static int _mutable;\n"
            + "}";

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a nested record sorts before a nested union (records before unions).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordBeforeUnionAsync()
        => await Verify.VerifyAnalyzerAsync(
            "public class C\n{\n"
            + "    public class U : System.Runtime.CompilerServices.IUnion { }\n"
            + "    public record {|SST1201:R|} { }\n"
            + "}\n"
            + "namespace System.Runtime.CompilerServices\n{\n"
            + "    internal interface IUnion { }\n"
            + "    internal static class IsExternalInit { }\n"
            + "}");

    /// <summary>Verifies the move fix is not offered when the file has conditional directives.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalDirectivesSuppressFixAsync()
    {
        const string Source = "public class C\n{\n"
            + "#if DEBUG\n"
            + "    public const int Flag = 1;\n"
            + "#endif\n"
            + "    public void Method() { }\n"
            + "    private int {|SST1201:_field|};\n"
            + "}";

        var test = new Verify.Test
        {
            TestCode = Source,
            FixedCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies member-ordering can detect when union-marker resolution may be needed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnionCandidateScanFindsNestedReferenceTypesAsync()
    {
        var type = ParseType(
            "public class C { public sealed class Nested { } public record R; public int Value; }");

        await Assert.That(MemberOrderingAnalyzer.HasUnionCandidateMembers(type.Members)).IsTrue();
    }

    /// <summary>Verifies member-ordering skips union-marker resolution when only non-reference nested members exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnionCandidateScanSkipsNonReferenceMembersAsync()
    {
        var type = ParseType(
            "public class C { public int Value; public readonly record struct Item(int Value); public interface I { } }");

        await Assert.That(MemberOrderingAnalyzer.HasUnionCandidateMembers(type.Members)).IsFalse();
    }

    /// <summary>Verifies member-ordering can read the relevant modifier facts in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ModifierFactsCaptureStaticReadonlyAccessibilityAsync()
    {
        var field = ParseField("public static readonly int Value;");
        var facts = MemberOrder.ReadModifierFacts(field.Modifiers);

        await Assert.That(facts.IsPublic).IsTrue();
        await Assert.That(facts.IsStatic).IsTrue();
        await Assert.That(facts.IsReadOnly).IsTrue();
    }

    /// <summary>Verifies kind ranking maps ordered member declarations to their StyleCop families.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KindRankMapsOrderedMemberKindsAsync()
    {
        await Assert.That(MemberOrder.KindRank(SyntaxKind.FieldDeclaration, isUnion: false)).IsEqualTo(0);
        await Assert.That(MemberOrder.KindRank(SyntaxKind.MethodDeclaration, isUnion: false)).IsEqualTo(9);
        await Assert.That(MemberOrder.KindRank(SyntaxKind.RecordDeclaration, isUnion: false)).IsEqualTo(12);
    }

    /// <summary>Verifies kind ranking places nested unions after records and classes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KindRankMapsUnionAfterRecordAsync()
        => await Assert.That(MemberOrder.KindRank(SyntaxKind.ClassDeclaration, isUnion: true)).IsEqualTo(13);

    /// <summary>Verifies direct member-order comparisons stop at the first differing dimension.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareDimensionsUsesFirstDifferenceAsync()
    {
        var left = new MemberOrder(7, 0, 1, 1, 1);
        var right = new MemberOrder(7, 3, 0, 0, 0);

        await Assert.That(MemberOrder.CompareDimensions(left, right)).IsLessThan(0);
    }

    /// <summary>Verifies readonly violations map to the instance-specific rule for instance fields.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelectViolationRuleUsesInstanceReadonlyRuleAsync()
    {
        var previous = new MemberOrder(0, 5, 1, 1, 1);
        var current = new MemberOrder(0, 5, 1, 1, 0);

        await Assert.That(MemberOrder.SelectViolationRule(current, previous)?.Id)
            .IsEqualTo(OrderingRules.InstanceReadonlyBeforeNonReadonly.Id);
    }

    /// <summary>Parses one type declaration for helper-level member-ordering tests.</summary>
    /// <param name="source">The type declaration source.</param>
    /// <returns>The parsed type declaration.</returns>
    private static TypeDeclarationSyntax ParseType(string source)
        => (TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0];

    /// <summary>Parses one field declaration for helper-level member-ordering tests.</summary>
    /// <param name="source">The field declaration source.</param>
    /// <returns>The parsed field declaration.</returns>
    private static FieldDeclarationSyntax ParseField(string source)
        => (FieldDeclarationSyntax)SyntaxFactory.ParseCompilationUnit("public class C { " + source + " }")
            .Members[0]
            .ChildNodes()
            .Single();
}
