// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyUnnecessaryParentheses = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1459UnnecessaryParenthesesAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1459UnnecessaryParenthesesAnalyzer"/>.</summary>
public class UnnecessaryParenthesesAnalyzerUnitTest
{
    /// <summary>Verifies the syntax-only rule recognizes simple operands even before the source binds.</summary>
    /// <param name="expression">The standalone simple operand.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("new()")]
    [Arguments("base")]
    [Arguments("M<int>")]
    public Task UnboundSimpleOperandsAreReportedAsync(string expression) =>
        new VerifyUnnecessaryParentheses.Test
        {
            CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.None,
            TestCode = $$"""class C { object M() => {|SST1459:({{expression}})|}; }""",
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies only containers that isolate simple operands allow parentheses to be removed.</summary>
    /// <param name="body">The member body containing the marked operand.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("object M(object value) { return {|SST1459:(value)|}; }")]
    [Arguments("void M(System.Exception value) { throw {|SST1459:(value)|}; }")]
    [Arguments("void M(object value) { object copy = {|SST1459:(value)|}; }")]
    [Arguments("void M(object value) { M({|SST1459:(value)|}); }")]
    [Arguments("void M(object value) { value = {|SST1459:(this)|}; }")]
    [Arguments("object M() => {|SST1459:(new object())|};")]
    [Arguments("int M() => {|SST1459:(42)|};")]
    [Arguments("string M() => {|SST1459:(\"text\")|};")]
    [Arguments("char M() => {|SST1459:('x')|};")]
    [Arguments("bool M() => {|SST1459:(true)|};")]
    [Arguments("bool M() => {|SST1459:(false)|};")]
    [Arguments("object M() => {|SST1459:(null)|};")]
    [Arguments("object M() => {|SST1459:(default)|};")]
    [Arguments("int M(int[] values) => {|SST1459:(values[0])|};")]
    [Arguments("int M(string value) => {|SST1459:(value.Length)|};")]
    [Arguments("string M() => {|SST1459:(ToString())|};")]
    [Arguments("int M(int value) => (value) + 1;")]
    [Arguments("void M(int value) { (value) = 1; }")]
    [Arguments("int M(int value) => (value + 1);")]
    [Arguments("int M(int value) => ((value));")]
    public Task ParentContextControlsDiagnosticsAsync(string body) =>
        VerifyUnnecessaryParentheses.VerifyAnalyzerAsync($$"""class C { {{body}} }""");

    /// <summary>Verifies a return value wrapped in non-grouping parentheses is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StandaloneReturnValueIsReportedAsync() =>
        VerifyUnnecessaryParentheses.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int value) => {|SST1459:(value)|};
            }
            """);

    /// <summary>Verifies parentheses that declare precedence are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrecedenceParenthesesAreCleanAsync() =>
        VerifyUnnecessaryParentheses.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int left, int right) => (left + right) * 2;
            }
            """);
}
