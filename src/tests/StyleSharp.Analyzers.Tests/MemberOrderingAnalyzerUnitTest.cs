// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
            """
            public class C
            {
                private int _field;
                public C() { }
                public int Property { get; set; }
                public void Method() { }
            }
            """);

    /// <summary>Verifies a field after a method is reported (SST1201) and moved before it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KindOutOfOrderAsync()
    {
        const string Source = """
            public class C
            {
                public void Method() { }
                private int {|SST1201:_field|};
            }
            """;
        const string FixedSource = """
            public class C
            {
                private int _field;
                public void Method() { }
            }
            """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a more accessible member after a less accessible one of the same kind is reported (SST1202) and moved up.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AccessOutOfOrderAsync()
    {
        const string Source = """
            public class C
            {
                private void A() { }
                public void {|SST1202:B|}() { }
            }
            """;
        const string FixedSource = """
            public class C
            {
                public void B() { }
                private void A() { }
            }
            """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a constant after a field of the same accessibility is reported (SST1203) and moved up.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantAfterFieldAsync()
    {
        const string Source = """
            public class C
            {
                private int _field;
                private const int {|SST1203:Max|} = 1;
            }
            """;
        const string FixedSource = """
            public class C
            {
                private const int Max = 1;
                private int _field;
            }
            """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a static member after an instance member of the same kind is reported (SST1204) and moved up.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticAfterInstanceAsync()
    {
        const string Source = """
            public class C
            {
                public void Instance() { }
                public static void {|SST1204:Shared|}() { }
            }
            """;
        const string FixedSource = """
            public class C
            {
                public static void Shared() { }
                public void Instance() { }
            }
            """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an instance readonly field after a non-readonly field of the same accessibility is reported (SST1215) and moved up.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceReadonlyAfterMutableAsync()
    {
        const string Source = """
            public class C
            {
                private int _mutable;
                private readonly int {|SST1215:_value|};
            }
            """;
        const string FixedSource = """
            public class C
            {
                private readonly int _value;
                private int _mutable;
            }
            """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a static readonly field after a static non-readonly field is reported (SST1214) and moved up.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticReadonlyAfterMutableAsync()
    {
        const string Source = """
            public class C
            {
                private static int _mutable;
                private static readonly int {|SST1214:_value|};
            }
            """;
        const string FixedSource = """
            public class C
            {
                private static readonly int _value;
                private static int _mutable;
            }
            """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a struct's <c>readonly</c> method is not treated as a readonly field (no SST1215).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadonlyMethodIsNotReadonlyFieldAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public struct S
            {
                public void Reset()
                {
                }

                public readonly bool Equals(S other) => true;
            }
            """);

    /// <summary>Verifies a nested <c>readonly struct</c> is not treated as a readonly field (no SST1215).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadonlyStructIsNotReadonlyFieldAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public struct Mutable
                {
                }

                public readonly struct Frozen
                {
                }
            }
            """);

    /// <summary>Verifies a nested record sorts before a nested union (records before unions).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordBeforeUnionAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public class U : System.Runtime.CompilerServices.IUnion { }
                public record {|SST1201:R|} { }
            }
            namespace System.Runtime.CompilerServices
            {
                internal interface IUnion { }
                internal static class IsExternalInit { }
            }
            """);

    /// <summary>Verifies the move fix is not offered when the file has conditional directives.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalDirectivesSuppressFixAsync()
    {
        const string Source = """
            public class C
            {
            #if DEBUG
                public const int Flag = 1;
            #endif
                public void Method() { }
                private int {|SST1201:_field|};
            }
            """;

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

    /// <summary>Verifies kind ranking maps ordered member declarations to their the analyzer families.</summary>
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
        => (FieldDeclarationSyntax)SyntaxFactory.ParseCompilationUnit($"public class C {{ {source} }}")
            .Members[0]
            .ChildNodes()
            .Single();
}
