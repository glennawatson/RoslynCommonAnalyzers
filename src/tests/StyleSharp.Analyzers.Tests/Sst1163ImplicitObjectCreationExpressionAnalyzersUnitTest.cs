// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0014 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1163 analyzer that requires implicit object creation arguments to be on unique lines.</summary>
public class Sst1163ImplicitObjectCreationExpressionAnalyzersUnitTest
{
    /// <summary>Verifies an implicit object creation with all arguments on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string Test = """
            public class Foo
            {
                public Foo(int a, int b)
                {
                }

                public static Foo Create() => new(1, 2);
            }
            """;

        await Verifysst0014.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies an implicit object creation with arguments split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string Test = """
            public class Foo
            {
                public Foo(int a, int b)
                {
                }

                public static Foo Create() => {|SST1163:new(
                    1, 2)|};
            }
            """;

        await Verifysst0014.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies the code fix rewrites the implicit object creation so each argument is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string Test = """
            public class Foo
            {
                public Foo(int a, int b)
                {
                }

                public static Foo Create() => {|SST1163:new(
                    1, 2)|};
            }
            """;

        const string FixedSource = """
            public class Foo
            {
                public Foo(int a, int b)
                {
                }

                public static Foo Create() => new(
                    1,
                    2);
            }
            """;

        await Verifysst0014.VerifyCodeFixAsync(Test, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every implicit object creation with split arguments in a single document.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Test = """
            public class Foo
            {
                public Foo(int a, int b)
                {
                }

                public static Foo First() => {|SST1163:new(
                    1, 2)|};

                public static Foo Second() => {|SST1163:new(
                    3, 4)|};

                public static Foo Third() => {|SST1163:new(
                    5, 6)|};
            }
            """;

        const string FixedSource = """
            public class Foo
            {
                public Foo(int a, int b)
                {
                }

                public static Foo First() => new(
                    1,
                    2);

                public static Foo Second() => new(
                    3,
                    4);

                public static Foo Third() => new(
                    5,
                    6);
            }
            """;

        await Verifysst0014.VerifyCodeFixAsync(Test, FixedSource);
    }
}
