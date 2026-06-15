// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0008 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1157AttributeArgumentMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst1157AttributeArgumentMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1157 analyzer that requires attribute argument lists to keep their parameters on unique lines.</summary>
public class Sst1157AttributeAnalyzersUnitTest
{
    /// <summary>Verifies an empty document produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyAsync()
    {
        var test = string.Empty;

        await Verifysst0008.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies a declaration with all parameters on a single line produces no diagnostics.</summary>
    /// <param name="number">The number of parameters to generate.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    [Arguments(7)]
    [Arguments(8)]
    public async Task ValidFlatEntryAsync(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.Attribute(number);
        var test = classGenerator.Generate();
        await Verifysst0008.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies a declaration with parameters split unevenly across lines reports the expected diagnostic.</summary>
    /// <param name="number">The number of parameters to generate.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    [Arguments(7)]
    [Arguments(8)]
    public async Task InvalidJaggedEntryAsync(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJagged(number);
        await Verifysst0008.VerifyAnalyzerAsync(test, Verifysst0008.Diagnostic("SST1157").WithSpan(startLine, startColumn, endLine, endColumn));
    }

    /// <summary>Verifies the code fix rewrites a jaggedly-formatted declaration into the staggered form.</summary>
    /// <param name="number">The number of parameters to generate.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    [Arguments(7)]
    [Arguments(8)]
    public async Task ValidCodeFixAsync(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJagged(number);
        var fixtest = GenerateStaggered(number);
        var expected = Verifysst0008.Diagnostic("SST1157").WithSpan(startLine, startColumn, endLine, endColumn);
        await Verifysst0008.VerifyCodeFixAsync(test, expected, fixtest);
    }

    /// <summary>Verifies Fix All rewrites every attribute argument list with split arguments in a single document.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Test = """
            using System;

            public sealed class MyAttribute : Attribute
            {
                public MyAttribute(int a, int b) { }
                public MyAttribute(int a, int b, int c) { }
            }

            public class Foo
            {
                [{|SST1157:My(
                    1, 2)|}]
                public void First() { }

                [{|SST1157:My(
                    3, 4, 5)|}]
                public void Second() { }

                [{|SST1157:My(
                    6, 7)|}]
                public void Third() { }
            }
            """;

        const string FixedSource = """
            using System;

            public sealed class MyAttribute : Attribute
            {
                public MyAttribute(int a, int b) { }
                public MyAttribute(int a, int b, int c) { }
            }

            public class Foo
            {
                [My(
                    1,
                    2)]
                public void First() { }

                [My(
                    3,
                    4,
                    5)]
                public void Second() { }

                [My(
                    6,
                    7)]
                public void Third() { }
            }
            """;

        await Verifysst0008.VerifyCodeFixAsync(Test, FixedSource);
    }

    /// <summary>Builds a fixture whose parameters are split unevenly across lines along with the expected diagnostic span.</summary>
    /// <param name="number">The number of parameters to generate.</param>
    /// <returns>The generated source text and the expected diagnostic span.</returns>
    private static (string Text, int StartLine, int StartColumn, int EndLine, int EndColumn) GenerateJagged(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        var (startLine, startColumn, endLine, endColumn) = classGenerator.AttributeJagged(number);
        return (classGenerator.Generate(), startLine, startColumn, endLine, endColumn);
    }

    /// <summary>Builds a fixture with each parameter on its own line.</summary>
    /// <param name="number">The number of parameters to generate.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateStaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.AttributeStaggered(number);
        return classGenerator.Generate();
    }
}
