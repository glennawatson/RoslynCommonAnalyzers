// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0017 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst0017LocalFunctionStatementParameterMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst0017LocalFunctionStatementParameterMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST0017 analyzer that requires local function parameters to be on unique lines.</summary>
public class Sst0017LocalFunctionStatementAnalyzersUnitTest
{
    /// <summary>Verifies a local function with all parameters on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string test = """
            public class Foo
            {
                public void M()
                {
                    void Local(int a, int b)
                    {
                    }

                    Local(1, 2);
                }
            }
            """;

        await Verifysst0017.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies a local function with parameters split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string test = """
            public class Foo
            {
                public void M()
                {
                    {|SST0017:void Local(
                        int a, int b)
                    {
                    }|}

                    Local(1, 2);
                }
            }
            """;

        await Verifysst0017.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies the code fix rewrites the local function so each parameter is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string test = """
            public class Foo
            {
                public void M()
                {
                    {|SST0017:void Local(
                        int a, int b)
                    {
                    }|}

                    Local(1, 2);
                }
            }
            """;

        const string fixedSource = """
            public class Foo
            {
                public void M()
                {
                    void Local(
                        int a,
                        int b)
                    {
                    }

                    Local(1, 2);
                }
            }
            """;

        await Verifysst0017.VerifyCodeFixAsync(test, fixedSource);
    }
}
