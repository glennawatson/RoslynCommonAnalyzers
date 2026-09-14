// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using VerifyPrimaryCtor = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1425PrimaryConstructorParameterMutationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1425 (do not reassign captured primary-constructor parameters).</summary>
public class PrimaryConstructorParameterMutationAnalyzerUnitTest
{
    /// <summary>Verifies every registered compound assignment and unary mutation reports the parameter.</summary>
    /// <param name="mutation">The mutation expression with its expected diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("{|SST1425:count|} += 1")]
    [Arguments("{|SST1425:count|} -= 1")]
    [Arguments("{|SST1425:count|} *= 2")]
    [Arguments("{|SST1425:count|} /= 2")]
    [Arguments("{|SST1425:count|} %= 2")]
    [Arguments("{|SST1425:count|} &= 1")]
    [Arguments("{|SST1425:count|} ^= 1")]
    [Arguments("{|SST1425:count|} |= 1")]
    [Arguments("{|SST1425:count|} <<= 1")]
    [Arguments("{|SST1425:count|} >>= 1")]
    [Arguments("++{|SST1425:count|}")]
    [Arguments("--{|SST1425:count|}")]
    [Arguments("{|SST1425:count|}--")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CompoundAndUnaryMutationsAreReportedAsync(string mutation) =>
        VerifyPrimaryCtor.VerifyAnalyzerAsync($"class C(int count) {{ void M() {{ {mutation}; }} }}");

    /// <summary>Verifies nullable and generic primary parameters remain mutation candidates.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NullableAndGenericParametersAreReportedAsync() =>
        VerifyPrimaryCtor.VerifyAnalyzerAsync("""
            #nullable enable
            class C<T>(int first, T value, int? count) where T : class
            {
                void M(T replacement)
                {
                    {|SST1425:value|} = replacement;
                    {|SST1425:count|} ??= 1;
                }
            }
            """);

    /// <summary>Verifies a matching name must bind to the primary parameter rather than shadowing state.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ShadowingAndReadOnlyArgumentsAreCleanAsync() =>
        VerifyPrimaryCtor.VerifyAnalyzerAsync("""
            class C(int count, int value)
            {
                void Parameter(int count) { count = 0; }
                void Local() { int count = 0; count++; }
                void Lambda() { System.Action<int> action = count => count++; action(0); }
                int Property { set { value = 0; } }
                void Read() { Consume(count); Observe(in count); }
                void Member() { this.Value = 0; }
                int Value;
                static void Consume(int value) { }
                static void Observe(in int value) { }
                class Nested { void M(int count) { count = 0; } }
            }
            """);

    /// <summary>Verifies detached names and interface-contained expressions have no primary constructor.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExpressionsWithoutClassOrStructParametersAreRejectedAsync()
    {
        var detached = SyntaxFactory.IdentifierName("count");
        var root = SyntaxFactory.ParseCompilationUnit("interface I { void M(int count) { count = 0; } }");
        var assignment = root.DescendantNodes().OfType<AssignmentExpressionSyntax>().Single();
        await Assert.That(Sst1425PrimaryConstructorParameterMutationAnalyzer.CouldReferencePrimaryConstructorParameter(detached)).IsFalse();
        await Assert.That(Sst1425PrimaryConstructorParameterMutationAnalyzer.CouldReferencePrimaryConstructorParameter(assignment.Left)).IsFalse();
    }

    /// <summary>Verifies an empty primary constructor does not turn method parameters into captured state.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EmptyPrimaryConstructorIsCleanAsync() =>
        VerifyPrimaryCtor.VerifyAnalyzerAsync("class C() { void M(int count) { count = 0; } }");

    /// <summary>Verifies the syntax precheck rejects non-matching identifiers before semantic binding.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxCandidateCheckRejectsNonMatchingIdentifierAsync()
    {
        var expression = ParseExpression(
            "public class Counter(int count) { private int _value; public void Reset() { _value = 0; } }",
            SelectSecondMemberAssignmentLeft);

        await Assert.That(Sst1425PrimaryConstructorParameterMutationAnalyzer.CouldReferencePrimaryConstructorParameter(expression)).IsFalse();
    }

    /// <summary>Verifies the syntax precheck keeps matching identifiers inside class primary constructors.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxCandidateCheckKeepsMatchingIdentifierAsync()
    {
        var expression = ParseExpression(
            "public class Counter(int count) { public void Reset() { count = 0; } }",
            SelectFirstMemberAssignmentLeft);

        await Assert.That(Sst1425PrimaryConstructorParameterMutationAnalyzer.CouldReferencePrimaryConstructorParameter(expression)).IsTrue();
    }

    /// <summary>Verifies the syntax precheck ignores record primary constructor parameters.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxCandidateCheckRejectsRecordPrimaryConstructorIdentifierAsync()
    {
        var expression = ParseExpression(
            "public record Counter(int Count) { public Counter Reset() => this with { Count = 0 }; }",
            SelectRecordWithExpressionIdentifier);

        await Assert.That(Sst1425PrimaryConstructorParameterMutationAnalyzer.CouldReferencePrimaryConstructorParameter(expression)).IsFalse();
    }

    /// <summary>Verifies assignment and increment on a class primary-constructor parameter are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ClassPrimaryConstructorMutationReportedAsync() =>
        VerifyPrimaryCtor.VerifyAnalyzerAsync(
            """
            public class Counter(int count)
            {
                public void Reset()
                {
                    {|SST1425:count|} = 0;
                    {|SST1425:count|}++;
                }
            }
            """);

    /// <summary>Verifies <c>ref</c>/<c>out</c> passing of a struct primary-constructor parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StructPrimaryConstructorRefOutReportedAsync() =>
        VerifyPrimaryCtor.VerifyAnalyzerAsync(
            """
            public struct Counter(int count)
            {
                public void Reset()
                {
                    Bump(ref {|SST1425:count|});
                    Set(out {|SST1425:count|});
                }

                private static void Bump(ref int value) => value++;

                private static void Set(out int value) => value = 0;
            }
            """);

    /// <summary>Verifies record primary-constructor parameters are not reported because they are properties, not captured mutable parameters.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RecordPrimaryConstructorMutationIsCleanAsync() =>
        VerifyPrimaryCtor.VerifyAnalyzerAsync(
            """
            public record Counter(int Count)
            {
                public Counter Reset() => this with { Count = 0 };
            }

            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """);

    /// <summary>Verifies ordinary method parameters are not reported by SST1425.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OrdinaryMethodParameterIsCleanAsync() =>
        VerifyPrimaryCtor.VerifyAnalyzerAsync(
            """
            public class Counter
            {
                public void Reset(int count)
                {
                    count = 0;
                    count++;
                }
            }
            """);

    /// <summary>Parses the requested expression from a compilation unit for helper-level tests.</summary>
    /// <param name="source">The source containing the target expression.</param>
    /// <param name="selector">Selects the desired expression from the parsed compilation unit.</param>
    /// <returns>The parsed expression.</returns>
    private static ExpressionSyntax ParseExpression(string source, Func<CompilationUnitSyntax, ExpressionSyntax> selector)
    {
        var root = SyntaxFactory.ParseCompilationUnit(source);
        return selector(root);
    }

    /// <summary>Selects the left side of the first assignment in the first member method.</summary>
    /// <param name="root">The parsed compilation unit.</param>
    /// <returns>The selected expression.</returns>
    private static ExpressionSyntax SelectFirstMemberAssignmentLeft(CompilationUnitSyntax root)
    {
        var method = (MethodDeclarationSyntax)((ClassDeclarationSyntax)root.Members[0]).Members[0];
        return ((AssignmentExpressionSyntax)((ExpressionStatementSyntax)method.Body!.Statements[0]).Expression).Left;
    }

    /// <summary>Selects the left side of the first assignment in the second member method.</summary>
    /// <param name="root">The parsed compilation unit.</param>
    /// <returns>The selected expression.</returns>
    private static ExpressionSyntax SelectSecondMemberAssignmentLeft(CompilationUnitSyntax root)
    {
        var method = (MethodDeclarationSyntax)((ClassDeclarationSyntax)root.Members[0]).Members[1];
        return ((AssignmentExpressionSyntax)((ExpressionStatementSyntax)method.Body!.Statements[0]).Expression).Left;
    }

    /// <summary>Selects the identifier on the left side of the record's <c>with</c> initializer assignment.</summary>
    /// <param name="root">The parsed compilation unit.</param>
    /// <returns>The selected identifier expression.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExpressionSyntax SelectRecordWithExpressionIdentifier(CompilationUnitSyntax root) =>
        ((AssignmentExpressionSyntax)((WithExpressionSyntax)((MethodDeclarationSyntax)((RecordDeclarationSyntax)root.Members[0]).Members[0])
            .ExpressionBody!.Expression).Initializer!.Expressions[0]).Left;
}
