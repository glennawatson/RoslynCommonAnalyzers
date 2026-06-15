// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0013 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1162StructDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst1162StructDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1162 analyzer that requires struct declaration primary constructor parameters to be on unique lines.</summary>
public class Sst1162StructDeclarationAnalyzersUnitTest
{
    /// <summary>Verifies a struct declaration with all primary constructor parameters on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string Test = "public struct Foo(int a, int b);";

        await Verifysst0013.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies a struct declaration with primary constructor parameters split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string Test = """
            {|SST1162:public struct Foo(
                int a, int b);|}
            """;

        await Verifysst0013.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies the code fix rewrites the struct declaration so each primary constructor parameter is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string Test = """
            {|SST1162:public struct Foo(
                int a, int b);|}
            """;

        const string FixedSource = """
            public struct Foo(
                int a,
                int b);
            """;

        await Verifysst0013.VerifyCodeFixAsync(Test, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every struct declaration with split primary constructor parameters in a single document.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Test = """
            {|SST1162:public struct Foo(
                int a, int b);|}
            {|SST1162:public struct Bar(
                int c, int d);|}
            {|SST1162:public struct Baz(
                int e, int f);|}
            """;

        const string FixedSource = """
            public struct Foo(
                int a,
                int b);
            public struct Bar(
                int c,
                int d);
            public struct Baz(
                int e,
                int f);
            """;

        await Verifysst0013.VerifyCodeFixAsync(Test, FixedSource);
    }
}
