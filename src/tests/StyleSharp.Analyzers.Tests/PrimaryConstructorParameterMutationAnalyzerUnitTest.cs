// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq;

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
            static root => root.DescendantNodes().OfType<AssignmentExpressionSyntax>().Single().Left);

        await Assert.That(PrimaryConstructorParameterMutationAnalyzer.CouldReferencePrimaryConstructorParameter(expression)).IsFalse();
    }

    /// <summary>Verifies the syntax precheck keeps matching identifiers inside class primary constructors.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxCandidateCheckKeepsMatchingIdentifierAsync()
    {
        var expression = ParseExpression(
            "public class Counter(int count) { public void Reset() { count = 0; } }",
            static root => root.DescendantNodes().OfType<AssignmentExpressionSyntax>().Single().Left);

        await Assert.That(PrimaryConstructorParameterMutationAnalyzer.CouldReferencePrimaryConstructorParameter(expression)).IsTrue();
    }

    /// <summary>Verifies the syntax precheck ignores record primary constructor parameters.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxCandidateCheckRejectsRecordPrimaryConstructorIdentifierAsync()
    {
        var expression = ParseExpression(
            "public record Counter(int Count) { public Counter Reset() => this with { Count = 0 }; }",
            static root => root.DescendantNodes().OfType<IdentifierNameSyntax>().Single(static node => node.Identifier.ValueText == "Count"));

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
}
