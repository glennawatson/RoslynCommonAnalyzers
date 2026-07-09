// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUnreachableCode = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1453UnreachableCodeAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1453UnreachableCodeAnalyzer"/>.</summary>
public class UnreachableCodeAnalyzerUnitTest
{
    /// <summary>Verifies statements after a return are reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StatementAfterReturnIsReportedAsync()
        => await VerifyUnreachableCode.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M()
                {
                    return 1;
                    {|SST1453:System.Console.WriteLine(1);|}
                }
            }
            """);

    /// <summary>Verifies sequential reachable statements are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ReachableStatementsAreCleanAsync()
        => await VerifyUnreachableCode.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M()
                {
                    System.Console.WriteLine(1);
                    return 1;
                }
            }
            """);

    /// <summary>Verifies a local function declared after a return is clean; the compiler hoists it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LocalFunctionAfterReturnIsCleanAsync()
        => await VerifyUnreachableCode.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int[] values)
                {
                    var total = 0;
                    foreach (var v in values)
                    {
                        total = Add(total, v);
                    }

                    return total;

                    static int Add(int a, int b) => a + b;
                }
            }
            """);

    /// <summary>Verifies a local function declared after a throw is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LocalFunctionAfterThrowIsCleanAsync()
        => await VerifyUnreachableCode.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M()
                {
                    throw new System.InvalidOperationException(Describe());

                    static string Describe() => "boom";
                }
            }
            """);

    /// <summary>Verifies a statement after a hoisted local function is still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StatementAfterLocalFunctionIsReportedAsync()
        => await VerifyUnreachableCode.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M()
                {
                    return;

                    static int F() => 1;

                    {|SST1453:System.Console.WriteLine(F());|}
                }
            }
            """);

    /// <summary>Verifies a labeled statement and what follows it stay clean; a goto can reach them.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LabeledStatementAfterReturnIsCleanAsync()
        => await VerifyUnreachableCode.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(bool condition)
                {
                    if (condition)
                    {
                        goto end;
                    }

                    return 1;
                end:
                    System.Console.WriteLine(1);
                    return 2;
                }
            }
            """);
}
