// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifyrcgs0016 = Blazor.Common.Analyzers.Tests.CSharpCodeFixVerifier<
    Blazor.Common.Analyzers.Rcgs0016PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesAnalyzer,
    Blazor.Common.Analyzers.Rcgs0016PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesCodeFixProvider>;

namespace Blazor.Common.Analyzers.Tests;

/// <summary>Unit tests for the RCGS0016 analyzer that requires primary constructor base type arguments to be on unique lines.</summary>
public class Rcgs0016PrimaryConstructorBaseTypeAnalyzersUnitTest
{
    /// <summary>Verifies a primary constructor base type with all arguments on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string test = """
            public record Bar(int x, int y);
            public record Foo(int a, int b) : Bar(a, b);
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        await Verifyrcgs0016.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies a primary constructor base type with arguments split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string test = """
            public record Bar(int x, int y);
            public record Foo(int a, int b) : {|RCGS0016:Bar(
                a, b)|};
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        await Verifyrcgs0016.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies the code fix rewrites the primary constructor base type so each argument is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string test = """
            public record Bar(int x, int y);
            public record Foo(int a, int b) : {|RCGS0016:Bar(
                a, b)|};
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        const string fixedSource = """
            public record Bar(int x, int y);
            public record Foo(int a, int b) : Bar(
                a,
                b);
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        await Verifyrcgs0016.VerifyCodeFixAsync(test, fixedSource);
    }
}
