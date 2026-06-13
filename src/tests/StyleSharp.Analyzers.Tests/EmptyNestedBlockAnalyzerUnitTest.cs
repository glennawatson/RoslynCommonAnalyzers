// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEmptyBlock = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.EmptyCodeAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1439 (empty nested blocks).</summary>
public class EmptyNestedBlockAnalyzerUnitTest
{
    /// <summary>Verifies an empty loop body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyLoopBodyReportedAsync()
        => await VerifyEmptyBlock.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M(bool flag)
                {
                    while (flag)
                    {|SST1439:{
                    }|}
                }
            }
            """);

    /// <summary>Verifies a non-empty loop body and an empty else clause are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonEmptyBodyAndEmptyElseAreCleanAsync()
        => await VerifyEmptyBlock.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M(bool flag)
                {
                    while (flag)
                    {
                        flag = false;
                    }

                    if (flag)
                    {
                        flag = false;
                    }
                    else
                    {
                    }
                }
            }
            """);
}
