// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifyrcgs0014 = Blazor.Common.Analyzers.Tests.CSharpCodeFixVerifier<
    Blazor.Common.Analyzers.Rcgs0014ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer,
    Blazor.Common.Analyzers.Rcgs0014ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider>;

namespace Blazor.Common.Analyzers.Tests;

/// <summary>Unit tests for the RCGS0014 analyzer that requires implicit object creation arguments to be on unique lines.</summary>
public class Rcgs0014ImplicitObjectCreationExpressionAnalyzersUnitTest
{
    /// <summary>Verifies an implicit object creation with all arguments on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string test = """
            public class Foo
            {
                public Foo(int a, int b)
                {
                }

                public static Foo Create() => new(1, 2);
            }
            """;

        await Verifyrcgs0014.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies an implicit object creation with arguments split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string test = """
            public class Foo
            {
                public Foo(int a, int b)
                {
                }

                public static Foo Create() => {|RCGS0014:new(
                    1, 2)|};
            }
            """;

        await Verifyrcgs0014.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies the code fix rewrites the implicit object creation so each argument is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string test = """
            public class Foo
            {
                public Foo(int a, int b)
                {
                }

                public static Foo Create() => {|RCGS0014:new(
                    1, 2)|};
            }
            """;

        const string fixedSource = """
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

        await Verifyrcgs0014.VerifyCodeFixAsync(test, fixedSource);
    }
}
