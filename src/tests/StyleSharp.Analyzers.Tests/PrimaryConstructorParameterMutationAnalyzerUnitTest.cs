// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using VerifyPrimaryCtor = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.PrimaryConstructorParameterMutationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1425 (do not reassign captured primary-constructor parameters).</summary>
public class PrimaryConstructorParameterMutationAnalyzerUnitTest
{
    /// <summary>Verifies the syntax precheck rejects non-matching identifiers before semantic binding.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxCandidateCheckRejectsNonMatchingIdentifierAsync()
    {
        var expression = ParseExpression(
            "public class Counter(int count) { private int _value; public void Reset() { _value = 0; } }",
            SelectSecondMemberAssignmentLeft);

        await Assert.That(PrimaryConstructorParameterMutationAnalyzer.CouldReferencePrimaryConstructorParameter(expression)).IsFalse();
    }

    /// <summary>Verifies the syntax precheck keeps matching identifiers inside class primary constructors.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxCandidateCheckKeepsMatchingIdentifierAsync()
    {
        var expression = ParseExpression(
            "public class Counter(int count) { public void Reset() { count = 0; } }",
            SelectFirstMemberAssignmentLeft);

        await Assert.That(PrimaryConstructorParameterMutationAnalyzer.CouldReferencePrimaryConstructorParameter(expression)).IsTrue();
    }

    /// <summary>Verifies the syntax precheck ignores record primary constructor parameters.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxCandidateCheckRejectsRecordPrimaryConstructorIdentifierAsync()
    {
        var expression = ParseExpression(
            "public record Counter(int Count) { public Counter Reset() => this with { Count = 0 }; }",
            SelectRecordWithExpressionIdentifier);

        await Assert.That(PrimaryConstructorParameterMutationAnalyzer.CouldReferencePrimaryConstructorParameter(expression)).IsFalse();
    }

    /// <summary>Verifies assignment and increment on a class primary-constructor parameter are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassPrimaryConstructorMutationReportedAsync()
        => await VerifyPrimaryCtor.VerifyAnalyzerAsync(
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
    [Test]
    public async Task StructPrimaryConstructorRefOutReportedAsync()
        => await VerifyPrimaryCtor.VerifyAnalyzerAsync(
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
    [Test]
    public async Task RecordPrimaryConstructorMutationIsCleanAsync()
        => await VerifyPrimaryCtor.VerifyAnalyzerAsync(
            """
            public record Counter(int Count)
            {
                public Counter Reset() => this with { Count = 0 };
            }

            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """);

    /// <summary>Verifies ordinary method parameters are not reported by SST1425.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrdinaryMethodParameterIsCleanAsync()
        => await VerifyPrimaryCtor.VerifyAnalyzerAsync(
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
    private static ExpressionSyntax SelectRecordWithExpressionIdentifier(CompilationUnitSyntax root)
        => ((AssignmentExpressionSyntax)((WithExpressionSyntax)((MethodDeclarationSyntax)((RecordDeclarationSyntax)root.Members[0]).Members[0])
            .ExpressionBody!.Expression).Initializer!.Expressions[0]).Left;
}
