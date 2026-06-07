// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0015 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1164ConstructorInitializerArgumentMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst1164ConstructorInitializerArgumentMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1164 analyzer that requires constructor initializer arguments to be on unique lines.</summary>
public class Sst1164ConstructorInitializerAnalyzersUnitTest
{
    /// <summary>Verifies a constructor initializer with all arguments on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string Test = """
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

        await Verifysst0015.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies a constructor initializer with arguments split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string Test = """
            public class Bar
            {
                public Bar(int a, int b)
                {
                }
            }

            public class Foo : Bar
            {
                public Foo()
                    {|SST1164:: base(
                        1, 2)|}
                {
                }
            }
            """;

        await Verifysst0015.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies the code fix rewrites the constructor initializer so each argument is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string Test = """
            public class Bar
            {
                public Bar(int a, int b)
                {
                }
            }

            public class Foo : Bar
            {
                public Foo()
                    {|SST1164:: base(
                        1, 2)|}
                {
                }
            }
            """;

        const string FixedSource = """
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

        await Verifysst0015.VerifyCodeFixAsync(Test, FixedSource);
    }
}
