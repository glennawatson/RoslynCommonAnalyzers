// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyPragma = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1426PragmaWarningDisableAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1426 (prefer [SuppressMessage] over #pragma warning disable).</summary>
public class Sst1426PragmaWarningDisableAnalyzerUnitTest
{
    /// <summary>Verifies a <c>#pragma warning disable</c> of an analyzer code is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisableOfAnalyzerCodeReportedAsync()
        => await VerifyAsync(
            """
            internal class C
            {
                {|SST1426:#pragma warning disable SST1309|}
                private int field;
                #pragma warning restore SST1309
            }
            """);

    /// <summary>Verifies a <c>#pragma warning disable</c> of a compiler (CS) code is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompilerCodeDisableNotReportedAsync()
        => await VerifyAsync(
            """
            internal class C
            {
                #pragma warning disable CS1591
                private int field;
                #pragma warning restore CS1591
            }
            """);

    /// <summary>Verifies a <c>#pragma warning disable</c> of a bare numeric (compiler) code is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NumericCodeDisableNotReportedAsync()
        => await VerifyAsync(
            """
            internal class C
            {
                #pragma warning disable 0168
                private int field;
                #pragma warning restore 0168
            }
            """);

    /// <summary>Verifies a directive mixing a compiler code and an analyzer code is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedCompilerAndAnalyzerCodesReportedAsync()
        => await VerifyAsync(
            """
            internal class C
            {
                {|SST1426:#pragma warning disable CS1591, SST1309|}
                private int field;
                #pragma warning restore CS1591, SST1309
            }
            """);

    /// <summary>Verifies a directive listing several analyzer codes is reported once.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleAnalyzerCodesReportedAsync()
        => await VerifyAsync(
            """
            internal class C
            {
                {|SST1426:#pragma warning disable SST1309, SST1117|}
                private int field;
                #pragma warning restore SST1309, SST1117
            }
            """);

    /// <summary>Verifies an analyzer-code disable inside a method body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StatementLevelDisableReportedAsync()
        => await VerifyAsync(
            """
            internal class C
            {
                private void M()
                {
                    {|SST1426:#pragma warning disable SST1309|}
                    var x = 1;
                    #pragma warning restore SST1309
                }
            }
            """);

    /// <summary>Verifies code with no pragma directive is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoPragmaIsCleanAsync()
        => await VerifyAsync(
            """
            internal class C
            {
                private int field;
            }
            """);

    /// <summary>
    /// Runs the analyzer over <paramref name="source"/>, skipping the verifier's suppression check.
    /// SST1426 flags every analyzer-code <c>#pragma warning disable</c>, including the one the check
    /// injects to suppress SST1426 itself, so that generic check does not apply to this rule.
    /// </summary>
    /// <param name="source">The markup source to analyze.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyPragma.Test
        {
            TestCode = source,
            TestBehaviors = TestBehaviors.SkipSuppressionCheck,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
