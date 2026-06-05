// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0013 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst0013StructDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst0013StructDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST0013 analyzer that requires struct declaration primary constructor parameters to be on unique lines.</summary>
public class Sst0013StructDeclarationAnalyzersUnitTest
{
    /// <summary>Verifies a struct declaration with all primary constructor parameters on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string test = "public struct Foo(int a, int b);";

        await Verifysst0013.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies a struct declaration with primary constructor parameters split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string test = """
            {|SST0013:public struct Foo(
                int a, int b);|}
            """;

        await Verifysst0013.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies the code fix rewrites the struct declaration so each primary constructor parameter is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string test = """
            {|SST0013:public struct Foo(
                int a, int b);|}
            """;

        const string fixedSource = """
            public struct Foo(
                int a,
                int b);
            """;

        await Verifysst0013.VerifyCodeFixAsync(test, fixedSource);
    }
}
