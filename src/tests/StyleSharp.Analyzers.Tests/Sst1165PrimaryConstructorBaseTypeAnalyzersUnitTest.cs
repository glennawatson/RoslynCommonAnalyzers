// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0016 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1165 analyzer that requires primary constructor base type arguments to be on unique lines.</summary>
public class Sst1165PrimaryConstructorBaseTypeAnalyzersUnitTest
{
    /// <summary>Verifies a primary constructor base type with all arguments on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string Test = """
            public record Bar(int x, int y);
            public record Foo(int a, int b) : Bar(a, b);
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        await Verifysst0016.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies a primary constructor base type with arguments split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string Test = """
            public record Bar(int x, int y);
            public record Foo(int a, int b) : {|SST1165:Bar(
                a, b)|};
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        await Verifysst0016.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies the code fix rewrites the primary constructor base type so each argument is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string Test = """
            public record Bar(int x, int y);
            public record Foo(int a, int b) : {|SST1165:Bar(
                a, b)|};
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        const string FixedSource = """
            public record Bar(int x, int y);
            public record Foo(int a, int b) : Bar(
                a,
                b);
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        await Verifysst0016.VerifyCodeFixAsync(Test, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every primary constructor base type with split arguments in a single document.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Test = """
            public record Bar(int x, int y);
            public record Foo(int a, int b) : {|SST1165:Bar(
                a, b)|};
            public record Qux(int a, int b) : {|SST1165:Bar(
                a, b)|};
            public record Quux(int a, int b) : {|SST1165:Bar(
                a, b)|};
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        const string FixedSource = """
            public record Bar(int x, int y);
            public record Foo(int a, int b) : Bar(
                a,
                b);
            public record Qux(int a, int b) : Bar(
                a,
                b);
            public record Quux(int a, int b) : Bar(
                a,
                b);
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """;

        await Verifysst0016.VerifyCodeFixAsync(Test, FixedSource);
    }
}
