// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyChained = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.ExpressionSimplificationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1187 (chained assignments).</summary>
public class ChainedAssignmentAnalyzerUnitTest
{
    /// <summary>Verifies a chained assignment <c>a = b = c</c> is reported on the outer assignment.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ChainedAssignmentReportedAsync()
        => await VerifyChained.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M(int a, int b, int c)
                {
                    {|SST1187:a = b = c|};
                }
            }
            """);

    /// <summary>Verifies separate assignment statements are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparateAssignmentsAreCleanAsync()
        => await VerifyChained.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M(int a, int b, int c)
                {
                    b = c;
                    a = b;
                }
            }
            """);
}
