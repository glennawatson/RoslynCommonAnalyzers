// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0017 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1166LocalFunctionStatementParameterMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst1166LocalFunctionStatementParameterMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1166 analyzer that requires local function parameters to be on unique lines.</summary>
public class Sst1166LocalFunctionStatementAnalyzersUnitTest
{
    /// <summary>Verifies a local function with all parameters on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string Test = """
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

        await Verifysst0017.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies a local function with parameters split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string Test = """
            public class Foo
            {
                public void M()
                {
                    {|SST1166:void Local(
                        int a, int b)
                    {
                    }|}

                    Local(1, 2);
                }
            }
            """;

        await Verifysst0017.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies the code fix rewrites the local function so each parameter is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string Test = """
            public class Foo
            {
                public void M()
                {
                    {|SST1166:void Local(
                        int a, int b)
                    {
                    }|}

                    Local(1, 2);
                }
            }
            """;

        const string FixedSource = """
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

        await Verifysst0017.VerifyCodeFixAsync(Test, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every local function with split parameters in a single document.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Test = """
            public class Foo
            {
                public void M()
                {
                    {|SST1166:void First(
                        int a, int b)
                    {
                    }|}

                    {|SST1166:void Second(
                        int c, int d)
                    {
                    }|}

                    {|SST1166:void Third(
                        int e, int f)
                    {
                    }|}

                    First(1, 2);
                    Second(3, 4);
                    Third(5, 6);
                }
            }
            """;

        const string FixedSource = """
            public class Foo
            {
                public void M()
                {
                    void First(
                        int a,
                        int b)
                    {
                    }

                    void Second(
                        int c,
                        int d)
                    {
                    }

                    void Third(
                        int e,
                        int f)
                    {
                    }

                    First(1, 2);
                    Second(3, 4);
                    Third(5, 6);
                }
            }
            """;

        await Verifysst0017.VerifyCodeFixAsync(Test, FixedSource);
    }
}
