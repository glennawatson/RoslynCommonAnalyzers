// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifyrcgs0012 = Blazor.Common.Analyzers.Tests.CSharpCodeFixVerifier<
    Blazor.Common.Analyzers.Rcgs0012ClassDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    Blazor.Common.Analyzers.Rcgs0012ClassDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

namespace Blazor.Common.Analyzers.Tests;

/// <summary>Unit tests for the RCGS0012 analyzer that requires class declaration primary constructor parameters to be on unique lines.</summary>
public class Rcgs0012ClassDeclarationAnalyzersUnitTest
{
    /// <summary>Verifies a class declaration with all primary constructor parameters on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string test = "public class Foo(int a, int b);";

        await Verifyrcgs0012.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies a class declaration with primary constructor parameters split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string test = """
            {|RCGS0012:public class Foo(
                int a, int b);|}
            """;

        await Verifyrcgs0012.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies the code fix rewrites the class declaration so each primary constructor parameter is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string test = """
            {|RCGS0012:public class Foo(
                int a, int b);|}
            """;

        const string fixedSource = """
            public class Foo(
                int a,
                int b);
            """;

        await Verifyrcgs0012.VerifyCodeFixAsync(test, fixedSource);
    }
}
