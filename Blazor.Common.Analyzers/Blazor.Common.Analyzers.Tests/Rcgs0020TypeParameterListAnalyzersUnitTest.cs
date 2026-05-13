// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifyrcgs0020 = Blazor.Common.Analyzers.Tests.CSharpCodeFixVerifier<
    Blazor.Common.Analyzers.Rcgs0020TypeParameterListMustBeOnUniqueLinesAnalyzer,
    Blazor.Common.Analyzers.Rcgs0020TypeParameterListMustBeOnUniqueLinesCodeFixProvider>;

namespace Blazor.Common.Analyzers.Tests;

/// <summary>Unit tests for the RCGS0020 analyzer that requires type parameter lists to be on unique lines.</summary>
public class Rcgs0020TypeParameterListAnalyzersUnitTest
{
    /// <summary>Verifies a type parameter list with all parameters on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string test = """
            public class Foo<T1, T2>
            {
            }
            """;

        await Verifyrcgs0020.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies a type parameter list split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string test = """
            public class Foo{|RCGS0020:<
                T1, T2>|}
            {
            }
            """;

        await Verifyrcgs0020.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies the code fix rewrites the type parameter list so each parameter is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string test = """
            public class Foo{|RCGS0020:<
                T1, T2>|}
            {
            }
            """;

        const string fixedSource = """
            public class Foo<
                T1,
                T2>
            {
            }
            """;

        await Verifyrcgs0020.VerifyCodeFixAsync(test, fixedSource);
    }
}
