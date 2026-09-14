// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using VerifyEmptyJoin = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1215UseConcatOverEmptyJoinAnalyzer,
    PerformanceSharp.Analyzers.Psh1215UseConcatOverEmptyJoinCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1215 (concatenate when there is no separator) and its code fix.</summary>
public class UseConcatOverEmptyJoinAnalyzerUnitTest
{
    /// <summary>The minimal framework declarations used to vary the available string overloads.</summary>
    private const string FrameworkTypes = """
        namespace System
        {
            public class Object { }
            public class ValueType { }
            public struct Void { }
            public struct Int32 { }
            public struct Boolean { }
            public class Array { }
            public class Attribute { }
            public class ParamArrayAttribute : Attribute { }
            public struct ReadOnlySpan<T> { }
        }
        namespace System.Collections.Generic
        {
            public interface IEnumerable<T> { }
            public class List<T> { }
        }
        namespace Other { public struct ReadOnlySpan<T> { } }
        namespace Outer.System { public struct ReadOnlySpan<T> { } }
        class Container { public struct ReadOnlySpan<T> { } }
        """;

    /// <summary>Verifies <c>string.Join</c> with an empty literal separator over a string array is reported (PSH1215) and rewritten to <c>string.Concat</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyLiteralSeparatorWithArrayReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(string[] parts)
                                      => {|PSH1215:string.Join("", parts)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M(string[] parts)
                                           => string.Concat(parts);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies <c>string.Join</c> with a <c>string.Empty</c> separator over an <c>IEnumerable&lt;string&gt;</c> is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringEmptySeparatorWithEnumerableReplacedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public string M(IEnumerable<string> parts)
                                      => {|PSH1215:string.Join(string.Empty, parts)|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public string M(IEnumerable<string> parts)
                                           => string.Concat(parts);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the expanded params call form keeps every value argument: <c>Join("", a, b, c)</c> becomes <c>Concat(a, b, c)</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpandedParamsFormReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(string a, string b, string c)
                                      => {|PSH1215:string.Join("", a, b, c)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M(string a, string b, string c)
                                           => string.Concat(a, b, c);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the generic <c>Join&lt;T&gt;</c> shape over an <c>IEnumerable&lt;int&gt;</c> is reported and rewritten to the generic Concat.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericEnumerableReplacedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public string M(IEnumerable<int> numbers)
                                      => {|PSH1215:string.Join("", numbers)|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public string M(IEnumerable<int> numbers)
                                           => string.Concat(numbers);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a non-empty separator is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonEmptySeparatorIsCleanAsync() =>
        VerifyNet90CleanAsync(
            """
            public class C
            {
                public string M(string[] parts)
                    => string.Join(",", parts);
            }
            """);

    /// <summary>Verifies the start/count overload <c>Join(string, string[], int, int)</c> is not reported — it has no Concat equivalent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StartCountOverloadIsCleanAsync() =>
        VerifyNet90CleanAsync(
            """
            public class C
            {
                public string M(string[] parts)
                    => string.Join("", parts, 0, 2);
            }
            """);

    /// <summary>Verifies a char separator is not reported — it can never be the empty string.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CharSeparatorIsCleanAsync() =>
        VerifyNet90CleanAsync(
            """
            public class C
            {
                public string M(string[] parts)
                    => string.Join(' ', parts);
            }
            """);

    /// <summary>Verifies a user-defined <c>String.Join</c> method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UserDefinedJoinIsCleanAsync() =>
        VerifyNet90CleanAsync(
            """
            public static class String
            {
                public static string Join(string separator, params string[] values) => separator;
            }

            public class C
            {
                public string M(string[] parts)
                    => String.Join("", parts);
            }
            """);

    /// <summary>Verifies the syntax gate accepts only supported receivers and unnamed empty separators.</summary>
    /// <param name="expression">The invocation syntax to classify.</param>
    /// <param name="expected">Whether the invocation is a candidate.</param>
    /// <param name="isLiteral">Whether its separator is recognized as an empty literal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("string.Join(\"\", values)", true, true)]
    [Arguments("String.Join<string>(\"\", values)", true, true)]
    [Arguments("String.Join(string.Empty, values)", true, false)]
    [Arguments("Join(\"\", values)", false, false)]
    [Arguments("String->Join(\"\", values)", false, false)]
    [Arguments("string.Concat(\"\", values)", false, false)]
    [Arguments("int.Join(\"\", values)", false, false)]
    [Arguments("System.String.Join(\"\", values)", false, false)]
    [Arguments("value.Join(\"\", values)", false, false)]
    [Arguments("string.Join()", false, false)]
    [Arguments("string.Join(\"\")", false, false)]
    [Arguments("string.Join(separator: \"\", values: values)", false, false)]
    [Arguments("string.Join(\",\", values)", false, false)]
    [Arguments("string.Join(null, values)", false, false)]
    [Arguments("string.Join(separator, values)", false, false)]
    [Arguments("string.Join(String->Empty, values)", false, false)]
    [Arguments("string.Join(String.Empty<int>, values)", false, false)]
    [Arguments("string.Join(String.Other, values)", false, false)]
    public async Task CandidateRequiresSupportedSyntaxAsync(string expression, bool expected, bool isLiteral)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(expression);
        var actual = Psh1215UseConcatOverEmptyJoinAnalyzer.IsCandidate(invocation, out var separator, out var separatorIsLiteral);

        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(separator is not null).IsEqualTo(expected);
        await Assert.That(separatorIsLiteral).IsEqualTo(isLiteral);
    }

    /// <summary>Verifies non-field separators, foreign fields, instance calls, and unresolved calls stay silent.</summary>
    /// <param name="expression">The call in a runtime-backed compilation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("string.Join(Other.Empty, values)")]
    [Arguments("string.Join(OtherProperty.Empty, values)")]
    [Arguments("string.Join(Other.Property.Empty, values)")]
    [Arguments("string.Join(Other.Property.EmptyProperty, values)")]
    [Arguments("string.Join(Missing.Empty, values)")]
    [Arguments("string.Join(\"\", missing)")]
    [Arguments("String.Join(\"\", values)")]
    [Arguments("Other.Property?.Join(\"\", values)")]
    [Arguments("string.Join(Other.EmptyProperty, values)")]
    public async Task UnboundOrNonStringMemberIsCleanAsync(string expression)
    {
        var source = $$"""
            class Other
            {
                public static readonly string Empty = "";
                public static string EmptyProperty => "";
                public static Instance Property => null;
            }
            class Instance
            {
                public string Empty;
                public string EmptyProperty => "";
                public string Join(string separator, string[] values) => separator;
            }
            class OtherProperty { public static string Empty => ""; }
            class C { string M(string[] values, Instance String) => {{expression}}; }
            """;
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1215UseConcatOverEmptyJoinAnalyzer()]).GetAnalyzerDiagnosticsAsync();

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies each Join shape is reported only when its matching Concat overload exists.</summary>
    /// <param name="valuesType">The values parameter type.</param>
    /// <param name="generic">The generic parameter list of both methods.</param>
    /// <param name="hasMatchingConcat">Whether the matching replacement API is available.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("object[]", "", true)]
    [Arguments("object[]", "", false)]
    [Arguments("string[]", "", true)]
    [Arguments("string[]", "", false)]
    [Arguments("System.Collections.Generic.IEnumerable<string>", "", true)]
    [Arguments("System.Collections.Generic.IEnumerable<string>", "", false)]
    [Arguments("System.Collections.Generic.IEnumerable<T>", "<T>", true)]
    [Arguments("System.Collections.Generic.IEnumerable<T>", "<T>", false)]
    [Arguments("System.ReadOnlySpan<string>", "", true)]
    [Arguments("System.ReadOnlySpan<string>", "", false)]
    [Arguments("System.ReadOnlySpan<object>", "", true)]
    [Arguments("System.ReadOnlySpan<object>", "", false)]
    public async Task JoinRequiresMatchingConcatAsync(string valuesType, string generic, bool hasMatchingConcat)
    {
        var fallbackType = valuesType == "object[]" ? "string[]" : "object[]";
        var concatType = hasMatchingConcat ? valuesType : fallbackType;
        var concatGeneric = hasMatchingConcat ? generic : string.Empty;
        var members = $$"""
            public static string Join{{generic}}(string separator, {{valuesType}} values) => null;
            public static string Concat{{concatGeneric}}({{concatType}} values) => null;
            """;
        var source = FrameworkTypes + $$"""
            namespace System { public sealed class String { {{members}} } }
            class C<T> { string M({{valuesType}} values) => string.Join{{generic}}("", values); }
            """;
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(source)]);
        var tree = compilation.SyntaxTrees[0];
        var root = await tree.GetRootAsync();
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        await Assert.That(compilation.GetSemanticModel(tree).GetSymbolInfo(invocation).Symbol is IMethodSymbol).IsTrue();

        var diagnostics = await compilation.WithAnalyzers([new Psh1215UseConcatOverEmptyJoinAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(hasMatchingConcat ? 1 : 0);
        if (hasMatchingConcat)
        {
            await Assert.That(diagnostics[0].Id).IsEqualTo("PSH1215");
        }
    }

    /// <summary>Verifies unsupported value shapes are rejected despite another available Concat overload.</summary>
    /// <param name="valuesType">The unsupported values parameter type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int[]")]
    [Arguments("object")]
    [Arguments("T")]
    [Arguments("System.Collections.Generic.IEnumerable<int>")]
    [Arguments("System.ReadOnlySpan<int>")]
    [Arguments("System.Collections.Generic.List<string>")]
    [Arguments("Other.ReadOnlySpan<string>")]
    [Arguments("Outer.System.ReadOnlySpan<string>")]
    [Arguments("Container.ReadOnlySpan<string>")]
    public async Task UnsupportedJoinValuesAreCleanAsync(string valuesType)
    {
        var source = FrameworkTypes + $$"""
            namespace System
            {
                public sealed class String
                {
                    public static string Join<T>(string separator, {{valuesType}} values) => null;
                    public static string Concat(object[] values) => null;
                }
            }
            class C<T> { string M({{valuesType}} values) => string.Join<T>("", values); }
            """;
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(source)]);
        var tree = compilation.SyntaxTrees[0];
        var root = await tree.GetRootAsync();
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        await Assert.That(compilation.GetSemanticModel(tree).GetSymbolInfo(invocation).Symbol is IMethodSymbol).IsTrue();
        var diagnostics = await compilation.WithAnalyzers([new Psh1215UseConcatOverEmptyJoinAnalyzer()]).GetAnalyzerDiagnosticsAsync();

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a bound Join must have exactly the supported separator and enumerable contracts.</summary>
    /// <param name="member">The framework's Join declaration.</param>
    /// <param name="valuesType">The type of the caller's values parameter.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public static string Join(object separator, string[] values) => null;", "string[]")]
    [Arguments("public static string Join(string separator, string[] values, int extra = 0) => null;", "string[]")]
    [Arguments("public static string Join(string separator, System.Collections.Generic.IEnumerable<int> values) => null;", "System.Collections.Generic.IEnumerable<int>")]
    public async Task UnsupportedJoinContractIsCleanAsync(string member, string valuesType)
    {
        var source = FrameworkTypes + $$"""
            namespace System
            {
                public sealed class String
                {
                    {{member}}
                    public static string Concat(object[] values) => null;
                }
            }
            class C { string M({{valuesType}} values) => string.Join("", values); }
            """;
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(source)]);
        var tree = compilation.SyntaxTrees[0];
        var root = await tree.GetRootAsync();
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        await Assert.That(compilation.GetSemanticModel(tree).GetSymbolInfo(invocation).Symbol is IMethodSymbol).IsTrue();
        var diagnostics = await compilation.WithAnalyzers([new Psh1215UseConcatOverEmptyJoinAnalyzer()]).GetAnalyzerDiagnosticsAsync();

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies framework probing ignores members whose signatures cannot replace Join.</summary>
    /// <param name="member">The unsupported Concat member, or no member.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("public static object Concat;")]
    [Arguments("public string Concat(object[] values) => null;")]
    [Arguments("public static string Concat(object left, object right) => null;")]
    [Arguments("public static string Concat<T, U>(object[] values) => null;")]
    [Arguments("public static string Concat<T>(T values) => null;")]
    [Arguments("public static string Concat<T>(System.Collections.Generic.List<T> values) => null;")]
    [Arguments("public static string Concat(object values) => null;")]
    [Arguments("public static string Concat(int[] values) => null;")]
    [Arguments("public static string Concat(System.Collections.Generic.IEnumerable<int> values) => null;")]
    [Arguments("public static string Concat(System.Collections.Generic.List<string> values) => null;")]
    [Arguments("public static string Concat(System.ReadOnlySpan<int> values) => null;")]
    [Arguments("public static string Concat(Other.ReadOnlySpan<string> values) => null;")]
    public async Task UnavailableConcatLeavesJoinUnchangedAsync(string member)
    {
        var source = FrameworkTypes + $$"""
            namespace System
            {
                public sealed class String
                {
                    {{member}}
                    public static string Join(string separator, string[] values) => null;
                }
            }
            class C { string M(string[] values) => string.Join("", values); }
            """;
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(source)]);
        await Assert.That(Psh1215UseConcatOverEmptyJoinAnalyzer.ConcatOverloads.Resolve(compilation).HasAny).IsFalse();

        var diagnostics = await compilation.WithAnalyzers([new Psh1215UseConcatOverEmptyJoinAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies successive Join candidates reuse both available and unavailable overload results.</summary>
    /// <param name="hasConcat">Whether the framework exposes the matching Concat overload.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task MultipleCandidatesReuseResolvedOverloadsAsync(bool hasConcat)
    {
        const int CandidateCount = 2;
        var member = hasConcat ? "public static string Concat(string[] values) => null;" : string.Empty;
        var source = FrameworkTypes + $$"""
            namespace System
            {
                public sealed class String
                {
                    {{member}}
                    public static string Join(string separator, string[] values) => null;
                }
            }
            class C
            {
                string M(string[] values)
                {
                    string.Join("", values);
                    return string.Join("", values);
                }
            }
            """;
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(source)]);
        var diagnostics = await compilation.WithAnalyzers([new Psh1215UseConcatOverEmptyJoinAnalyzer()]).GetAnalyzerDiagnosticsAsync();

        await Assert.That(diagnostics.Length).IsEqualTo(hasConcat ? CandidateCount : 0);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyEmptyJoin.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task VerifyNet90CleanAsync(string source) =>
        VerifyNet90Async(source, source);
}
