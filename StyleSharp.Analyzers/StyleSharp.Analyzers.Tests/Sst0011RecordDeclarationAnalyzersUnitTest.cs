// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0011 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst0011RecordDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst0011RecordDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST0011 analyzer that requires record declaration parameters to be on unique lines.</summary>
public class Sst0011RecordDeclarationAnalyzersUnitTest
{
    /// <summary>Verifies a record declaration with all parameters on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string test = """
            public record Foo(int a, int b);
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        await Verifysst0011.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies a record declaration with parameters split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string test = """
            {|SST0011:public record Foo(
                int a, int b);|}
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        await Verifysst0011.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies the code fix rewrites the record declaration so each parameter is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string test = """
            {|SST0011:public record Foo(
                int a, int b);|}
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        const string fixedSource = """
            public record Foo(
                int a,
                int b);
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        await Verifysst0011.VerifyCodeFixAsync(test, fixedSource);
    }
}
