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
}
