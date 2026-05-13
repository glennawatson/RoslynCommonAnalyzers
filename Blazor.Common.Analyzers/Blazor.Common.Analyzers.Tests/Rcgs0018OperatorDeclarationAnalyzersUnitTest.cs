// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifyrcgs0018 = Blazor.Common.Analyzers.Tests.CSharpCodeFixVerifier<
    Blazor.Common.Analyzers.Rcgs0018OperatorDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    Blazor.Common.Analyzers.Rcgs0018OperatorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

namespace Blazor.Common.Analyzers.Tests;

/// <summary>Unit tests for the RCGS0018 analyzer that requires operator declaration parameters to be on unique lines.</summary>
public class Rcgs0018OperatorDeclarationAnalyzersUnitTest
{
    /// <summary>Verifies an operator declaration with all parameters on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string test = """
            public class Foo
            {
                public static Foo operator +(Foo a, Foo b) => a;
            }
            """;

        await Verifyrcgs0018.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies an operator declaration with parameters split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string test = """
            public class Foo
            {
                {|RCGS0018:public static Foo operator +(
                    Foo a, Foo b) => a;|}
            }
            """;

        await Verifyrcgs0018.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies the code fix rewrites the operator declaration so each parameter is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string test = """
            public class Foo
            {
                {|RCGS0018:public static Foo operator +(
                    Foo a, Foo b) => a;|}
            }
            """;

        const string fixedSource = """
            public class Foo
            {
                public static Foo operator +(
                    Foo a,
                    Foo b) => a;
            }
            """;

        await Verifyrcgs0018.VerifyCodeFixAsync(test, fixedSource);
    }
}
