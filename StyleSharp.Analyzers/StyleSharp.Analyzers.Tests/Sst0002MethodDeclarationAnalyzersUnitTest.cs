// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0002 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst0002MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst0002MethodDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST0002 analyzer that requires method declarations to keep their parameters on unique lines.</summary>
public class Sst0002MethodDeclarationAnalyzersUnitTest
{
    /// <summary>Verifies an empty document produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyAsync()
    {
        var test = string.Empty;

        await Verifysst0002.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies a method declaration with all parameters on a single line produces no diagnostics.</summary>
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
        classGenerator.MethodDeclaration(number);
        var test = classGenerator.Generate();
        await Verifysst0002.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies a method declaration with parameters split unevenly across lines reports the expected diagnostic.</summary>
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
        await Verifysst0002.VerifyAnalyzerAsync(test, Verifysst0002.Diagnostic("SST0002").WithSpan(startLine, startColumn, endLine, endColumn));
    }

    /// <summary>Verifies the code fix rewrites a jaggedly-formatted method declaration into the staggered form.</summary>
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
        var expected = Verifysst0002.Diagnostic("SST0002").WithSpan(startLine, startColumn, endLine, endColumn);
        await Verifysst0002.VerifyCodeFixAsync(test, expected, fixtest);
    }

    /// <summary>Verifies an obsolete forwarding method with valid formatting produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidClassNoDiagnosticAsync()
    {
        const string Test = """
            using System;

            public static class Configuration
            {
                public static void AddDefaultHeader(string test, string test2)
                {
                }
            }

            public class Test
            {
                /// <summary>
                /// Add default header.
                /// </summary>
                /// <param name="key">Header field name.</param>
                /// <param name="value">Header field value.</param>
                [Obsolete("AddDefaultHeader is deprecated, please use Configuration.AddDefaultHeader instead.")]
                public void AddDefaultHeader(string key, string value)
                {
                    Configuration.AddDefaultHeader(key, value);
                }
            }

            """;

        await Verifysst0002.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Builds a fixture whose parameters are split unevenly across lines along with the expected diagnostic span.</summary>
    /// <param name="number">The number of parameters to generate.</param>
    /// <returns>The generated source text and the expected diagnostic span.</returns>
    private static (string Text, int StartLine, int StartColumn, int EndLine, int EndColumn) GenerateJagged(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        var (startLine, startColumn, endLine, endColumn) = classGenerator.MethodDeclarationJagged(number);
        return (classGenerator.Generate(), startLine, startColumn, endLine, endColumn);
    }

    /// <summary>Builds a fixture with each parameter on its own line.</summary>
    /// <param name="number">The number of parameters to generate.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateStaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.MethodDeclarationStaggered(number);
        return classGenerator.Generate();
    }
}
