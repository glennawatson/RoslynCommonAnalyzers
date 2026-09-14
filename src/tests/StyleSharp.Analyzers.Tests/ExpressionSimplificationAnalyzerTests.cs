// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyExpression = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.ExpressionSimplificationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests expression simplification across sequence, nullable and incomplete syntax shapes.</summary>
public class ExpressionSimplificationAnalyzerTests
{
    /// <summary>Verifies a cast supplying a default expression's type is retained.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CastsOfDefaultExpressionsAreRetainedAsync() =>
        VerifyExpression.VerifyAnalyzerAsync("class C { string M() => (string)default; string N() => (string)default(string); }");

    /// <summary>Verifies property initializers and optional parameters supply a type for the default literal.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonVariableInitializersUseDefaultLiteralAsync() =>
        VerifyExpression.VerifyAnalyzerAsync("class C { int P { get; } = {|SST1188:default(int)|}; void M(int value = {|SST1188:default(int)|}) { } }");

    /// <summary>Verifies each character requiring verbatim quoting keeps the verbatim string.</summary>
    /// <param name="literal">The verbatim literal and its expected diagnostic, if any.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("@\"left\\right\"")]
    [Arguments("@\"left\"\"right\"")]
    [Arguments("@\"left\nright\"")]
    [Arguments("@\"left\rright\"")]
    [Arguments("{|SST1184:@\"\t\"|}")]
    [Arguments("{|SST1184:@\"\v\"|}")]
    [Arguments("{|SST1184:@\"\f\"|}")]
    [Arguments("{|SST1184:@\"!#[]\"|}")]
    public Task VerbatimQuotingDependsOnDecodedCharactersAsync(string literal) =>
        VerifyExpression.VerifyAnalyzerAsync($"class C {{ string M() => {literal}; }}");

    /// <summary>Verifies different nested unary operators are not mistaken for doubled negation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MixedUnaryOperatorsAreRetainedAsync() =>
        VerifyExpression.VerifyAnalyzerAsync("class C { int M(int value) => ~(-value); int N(int value) => ~(-~value); }");

    /// <summary>Verifies array and implemented-interface element types identify redundant sequence casts.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ArrayAndCollectionSequenceCastsAreReportedAsync() =>
        VerifyExpression.VerifyAnalyzerAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                object Array(int[] values) => values.{|SST1175:Cast<int>|}();
                object List(List<int> values) => values.{|SST1175:Cast<int>|}();
                object Static(List<int> values) => Enumerable.{|SST1175:Cast<int>|}(values);
            }
            """);

    /// <summary>Verifies absent, ambiguous and nullable sequence element types keep their conversions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AmbiguousOrNullableSequenceCastsAreRetainedAsync() =>
        VerifyExpression.VerifyAnalyzerAsync("""
            #nullable enable
            using System.Collections;
            using System.Collections.Generic;
            using System.Linq;
            class Both : IEnumerable<int>, IEnumerable<string>
            {
                IEnumerator<int> IEnumerable<int>.GetEnumerator() => throw new System.NotImplementedException();
                IEnumerator<string> IEnumerable<string>.GetEnumerator() => throw new System.NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => throw new System.NotImplementedException();
            }
            class C
            {
                object Ambiguous(Both values) => values.Cast<int>();
                object Untyped(IEnumerable values) => values.Cast<int>();
                object Matrix(int[,] values) => values.Cast<int>();
                object Generic<T>(T values) where T : IEnumerable<int> => values.Cast<int>();
                object NullSource() => Enumerable.Cast<int>(null!);
                object NullableValues(int?[] values) => values.OfType<int?>();
                object NullabilityChange(IEnumerable<string?> values) => values.Cast<string>();
            }
            """);

    /// <summary>Verifies nested generic nullability is compared recursively for both cast forms.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NestedTypeArgumentNullabilityControlsCastReportingAsync() =>
        VerifyExpression.VerifyAnalyzerAsync("""
            #nullable enable
            using System.Collections.Generic;
            class C
            {
                object Same(IEnumerable<IEnumerable<string>> values) => ({|SST1175:IEnumerable<IEnumerable<string>>|})values;
                object Changed(IEnumerable<IEnumerable<string>> values) => (IEnumerable<IEnumerable<string?>>)values;
                object? AsChanged(IEnumerable<IEnumerable<string>> values) => values as IEnumerable<IEnumerable<string?>>;
                int? MaybeNull(int? value) => value as {|SST1175:int?|};
                string? Narrows(string? value) => value as string;
                string? Widens(string value) => (string?)value;
                string? Default() => default(string) as string;
                string? Null() => null as string;
            }
            """);

    /// <summary>Verifies lookalike sequence methods are screened by their declaring symbol.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonFrameworkSequenceMethodsAreRetainedAsync() =>
        VerifyExpression.VerifyAnalyzerAsync("""
            using System.Collections.Generic;
            static class Other
            {
                public static IEnumerable<T> Cast<T>(this C value) => throw new System.NotImplementedException();
            }
            class C
            {
                public object OfType<T>() => this;
                object M() => this.Cast<int>();
                object N() => this.OfType<int>();
            }
            """);

    /// <summary>Verifies unresolved calls and incomplete assignments do not invent a simplification.</summary>
    /// <param name="source">The incomplete source and any existing syntax-only diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("class C { object M() => Missing.Cast<int>(); }")]
    [Arguments("class C { string M() => (string)null; }")]
    [Arguments("class C { object M() => default as string; }")]
    [Arguments("class C { void M() { (x, y) = (x, y, z); } }")]
    [Arguments("class C { void M() { this.value = value; } }")]
    [Arguments("class C { void M() { default(int) = 1; } }")]
    public Task IncompleteExpressionsKeepCurrentDiagnosticsAsync(string source) =>
        new VerifyExpression.Test { TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None }.RunAsync(CancellationToken.None);

    /// <summary>Verifies syntax-only names can be inferred from conditional-access member bindings.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MemberBindingExposesItsInferredNameAsync()
    {
        var binding = SyntaxFactory.MemberBindingExpression(SyntaxFactory.IdentifierName("Value"));
        await Assert.That(ExpressionSimplificationAnalyzer.InferredName(binding)).IsEqualTo("Value");
    }

    /// <summary>Verifies a missing relational operand type does not classify it as nullable or floating point.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UntypedRelationalOperandIsNotClassifiedAsUnsafeAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { object M() => null; }");
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RoslynCommon.Analyzers.Tests.RuntimeMetadataReferences.Platform);
        var expression = (await tree.GetRootAsync()).DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax>().Single();
        await Assert.That(ExpressionSimplificationAnalyzer.IsUnsafeRelationalOperand(expression, compilation.GetSemanticModel(tree), CancellationToken.None)).IsFalse();
    }

    /// <summary>Verifies conditional access and the newer floating-point types prevent relational inversion.</summary>
    /// <param name="operandType">The floating-point operand type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("float")]
    [Arguments("System.Half")]
    [Arguments("System.Runtime.InteropServices.NFloat")]
    public Task FloatingPointRelationalInversionIsRetainedAsync(string operandType) =>
        new VerifyExpression.Test
        {
            TestCode = $"class C {{ bool M({operandType} left, {operandType} right) => !(left < right); bool N(string text) => !(text?.Length < 1); }}",
            ReferenceAssemblies = RoslynCommon.Analyzers.Tests.AnalyzerFrameworks.Net90,
        }.RunAsync(CancellationToken.None);
}
