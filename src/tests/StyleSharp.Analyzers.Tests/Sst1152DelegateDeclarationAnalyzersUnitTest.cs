// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0003 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1152DelegateDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst1152DelegateDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1152 analyzer that requires delegate declarations to keep their parameters on unique lines.</summary>
public class Sst1152DelegateDeclarationAnalyzersUnitTest
{
    /// <summary>Verifies an empty document produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyAsync()
    {
        var test = string.Empty;

        await Verifysst0003.VerifyAnalyzerAsync(test);
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
        classGenerator.DelegateDeclaration(number);
        var test = classGenerator.Generate();
        await Verifysst0003.VerifyAnalyzerAsync(test);
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
        await Verifysst0003.VerifyAnalyzerAsync(test, Verifysst0003.Diagnostic("SST1152").WithSpan(startLine, startColumn, endLine, endColumn));
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
        var expected = Verifysst0003.Diagnostic("SST1152").WithSpan(startLine, startColumn, endLine, endColumn);
        await Verifysst0003.VerifyCodeFixAsync(test, expected, fixtest);
    }

    /// <summary>Builds a fixture whose parameters are split unevenly across lines along with the expected diagnostic span.</summary>
    /// <param name="number">The number of parameters to generate.</param>
    /// <returns>The generated source text and the expected diagnostic span.</returns>
    private static (string Text, int StartLine, int StartColumn, int EndLine, int EndColumn) GenerateJagged(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        var (startLine, startColumn, endLine, endColumn) = classGenerator.DelegateDeclarationJagged(number);
        return (classGenerator.Generate(), startLine, startColumn, endLine, endColumn);
    }

    /// <summary>Builds a fixture with each parameter on its own line.</summary>
    /// <param name="number">The number of parameters to generate.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateStaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.DelegateDeclarationStaggered(number);
        return classGenerator.Generate();
    }
}
