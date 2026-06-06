// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRegion = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.RegionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the region rules (SST1123/SST1124).</summary>
public class RegionAnalyzerUnitTest
{
    /// <summary>Verifies a type-level region is reported as a region (SST1124) only.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeLevelRegionReportedAsync()
        => await VerifyRegion.VerifyAnalyzerAsync(
            """
            internal class C
            {
                {|SST1124:#region Fields|}
                private int field;
                #endregion
            }
            """);

    /// <summary>Verifies a region inside a method body is reported as both a region (SST1124) and a nested region (SST1123).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RegionWithinMethodReportedAsync()
        => await VerifyRegion.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M()
                {
                    {|SST1124:{|SST1123:#region Work|}|}
                    var x = 1;
                    #endregion
                }
            }
            """);

    /// <summary>Verifies code without regions is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoRegionIsCleanAsync()
        => await VerifyRegion.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int field;
            }
            """);
}
