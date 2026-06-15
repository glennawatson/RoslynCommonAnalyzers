// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0012 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1161ClassDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst1161ClassDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1161 analyzer that requires class declaration primary constructor parameters to be on unique lines.</summary>
public class Sst1161ClassDeclarationAnalyzersUnitTest
{
    /// <summary>Verifies a class declaration with all primary constructor parameters on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string Test = "public class Foo(int a, int b);";

        await Verifysst0012.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies a class declaration with primary constructor parameters split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string Test = """
            {|SST1161:public class Foo(
                int a, int b);|}
            """;

        await Verifysst0012.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies the code fix rewrites the class declaration so each primary constructor parameter is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string Test = """
            {|SST1161:public class Foo(
                int a, int b);|}
            """;

        const string FixedSource = """
            public class Foo(
                int a,
                int b);
            """;

        await Verifysst0012.VerifyCodeFixAsync(Test, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every class declaration with split primary constructor parameters in a single document.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Test = """
            {|SST1161:public class Foo(
                int a, int b);|}
            {|SST1161:public class Bar(
                int c, int d);|}
            {|SST1161:public class Baz(
                int e, int f);|}
            """;

        const string FixedSource = """
            public class Foo(
                int a,
                int b);
            public class Bar(
                int c,
                int d);
            public class Baz(
                int e,
                int f);
            """;

        await Verifysst0012.VerifyCodeFixAsync(Test, FixedSource);
    }
}
