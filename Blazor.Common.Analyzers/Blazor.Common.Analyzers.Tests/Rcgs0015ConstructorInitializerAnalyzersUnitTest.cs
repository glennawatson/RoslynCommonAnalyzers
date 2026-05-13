// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifyrcgs0015 = Blazor.Common.Analyzers.Tests.CSharpCodeFixVerifier<
    Blazor.Common.Analyzers.Rcgs0015ConstructorInitializerArgumentMustBeOnUniqueLinesAnalyzer,
    Blazor.Common.Analyzers.Rcgs0015ConstructorInitializerArgumentMustBeOnUniqueLinesCodeFixProvider>;

namespace Blazor.Common.Analyzers.Tests;

/// <summary>Unit tests for the RCGS0015 analyzer that requires constructor initializer arguments to be on unique lines.</summary>
public class Rcgs0015ConstructorInitializerAnalyzersUnitTest
{
    /// <summary>Verifies a constructor initializer with all arguments on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string test = """
            public class Bar
            {
                public Bar(int a, int b)
                {
                }
            }

            public class Foo : Bar
            {
                public Foo()
                    : base(1, 2)
                {
                }
            }
            """;

        await Verifyrcgs0015.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies a constructor initializer with arguments split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string test = """
            public class Bar
            {
                public Bar(int a, int b)
                {
                }
            }

            public class Foo : Bar
            {
                public Foo()
                    {|RCGS0015:: base(
                        1, 2)|}
                {
                }
            }
            """;

        await Verifyrcgs0015.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies the code fix rewrites the constructor initializer so each argument is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string test = """
            public class Bar
            {
                public Bar(int a, int b)
                {
                }
            }

            public class Foo : Bar
            {
                public Foo()
                    {|RCGS0015:: base(
                        1, 2)|}
                {
                }
            }
            """;

        const string fixedSource = """
            public class Bar
            {
                public Bar(int a, int b)
                {
                }
            }

            public class Foo : Bar
            {
                public Foo()
                    : base(
                        1,
                        2)
                {
                }
            }
            """;

        await Verifyrcgs0015.VerifyCodeFixAsync(test, fixedSource);
    }
}
