// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConstraint = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.ConstraintOnOwnLineAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the constraint-on-own-line rule (SST1127).</summary>
public class ConstraintOnOwnLineAnalyzerUnitTest
{
    /// <summary>Verifies a constraint sharing the declaration line is reported (SST1127).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstraintSharingDeclarationLineReportedAsync()
        => await VerifyConstraint.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M<T>() {|SST1127:where T : class|}
                {
                }
            }
            """);

    /// <summary>Verifies a constraint on its own line is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstraintOnOwnLineIsCleanAsync()
        => await VerifyConstraint.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M<T>()
                    where T : class
                {
                }
            }
            """);
}
