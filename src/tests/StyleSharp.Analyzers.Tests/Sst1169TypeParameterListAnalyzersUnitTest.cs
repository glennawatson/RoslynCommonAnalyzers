// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0020 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1169TypeParameterListMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst1169TypeParameterListMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1169 analyzer that requires type parameter lists to be on unique lines.</summary>
public class Sst1169TypeParameterListAnalyzersUnitTest
{
    /// <summary>Verifies a type parameter list with all parameters on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string Test = """
            public class Foo<T1, T2>
            {
            }
            """;

        await Verifysst0020.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies a type parameter list split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string Test = """
            public class Foo{|SST1169:<
                T1, T2>|}
            {
            }
            """;

        await Verifysst0020.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies the code fix rewrites the type parameter list so each parameter is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string Test = """
            public class Foo{|SST1169:<
                T1, T2>|}
            {
            }
            """;

        const string FixedSource = """
            public class Foo<
                T1,
                T2>
            {
            }
            """;

        await Verifysst0020.VerifyCodeFixAsync(Test, FixedSource);
    }
}
