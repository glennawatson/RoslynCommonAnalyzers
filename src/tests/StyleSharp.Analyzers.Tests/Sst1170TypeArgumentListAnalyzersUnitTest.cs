// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0021 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1170TypeArgumentListMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst1170TypeArgumentListMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1170 analyzer that requires type argument lists to be on unique lines.</summary>
public class Sst1170TypeArgumentListAnalyzersUnitTest
{
    /// <summary>Verifies a type argument list with all arguments on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string Test = """
            public class Foo
            {
                private readonly System.Collections.Generic.Dictionary<int, string> _map = new();
            }
            """;

        await Verifysst0021.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies a type argument list split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string Test = """
            public class Foo
            {
                private readonly System.Collections.Generic.Dictionary{|SST1170:<
                    int, string>|} _map = new();
            }
            """;

        await Verifysst0021.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies the code fix rewrites the type argument list so each argument is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string Test = """
            public class Foo
            {
                private readonly System.Collections.Generic.Dictionary{|SST1170:<
                    int, string>|} _map = new();
            }
            """;

        const string FixedSource = """
            public class Foo
            {
                private readonly System.Collections.Generic.Dictionary<
                    int,
                    string> _map = new();
            }
            """;

        await Verifysst0021.VerifyCodeFixAsync(Test, FixedSource);
    }
}
