// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifyrcgs0021 = Blazor.Common.Analyzers.Tests.CSharpCodeFixVerifier<
    Blazor.Common.Analyzers.Rcgs0021TypeArgumentListMustBeOnUniqueLinesAnalyzer,
    Blazor.Common.Analyzers.Rcgs0021TypeArgumentListMustBeOnUniqueLinesCodeFixProvider>;

namespace Blazor.Common.Analyzers.Tests;

/// <summary>Unit tests for the RCGS0021 analyzer that requires type argument lists to be on unique lines.</summary>
public class Rcgs0021TypeArgumentListAnalyzersUnitTest
{
    /// <summary>Verifies a type argument list with all arguments on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string test = """
            public class Foo
            {
                private readonly System.Collections.Generic.Dictionary<int, string> _map = new();
            }
            """;

        await Verifyrcgs0021.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies a type argument list split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string test = """
            public class Foo
            {
                private readonly System.Collections.Generic.Dictionary{|RCGS0021:<
                    int, string>|} _map = new();
            }
            """;

        await Verifyrcgs0021.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies the code fix rewrites the type argument list so each argument is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string test = """
            public class Foo
            {
                private readonly System.Collections.Generic.Dictionary{|RCGS0021:<
                    int, string>|} _map = new();
            }
            """;

        const string fixedSource = """
            public class Foo
            {
                private readonly System.Collections.Generic.Dictionary<
                    int,
                    string> _map = new();
            }
            """;

        await Verifyrcgs0021.VerifyCodeFixAsync(test, fixedSource);
    }
}
