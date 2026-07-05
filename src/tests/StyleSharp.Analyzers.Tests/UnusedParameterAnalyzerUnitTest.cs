// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUnusedParameter = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1461UnusedParameterAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1461UnusedParameterAnalyzer"/>.</summary>
public class UnusedParameterAnalyzerUnitTest
{
    /// <summary>Verifies an unused private method parameter is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PrivateMethodParameterIsReportedAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int M(int {|SST1461:value|}) => 1;
            }
            """);

    /// <summary>Verifies public method parameters are not reported because they are API surface.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PublicMethodParameterIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int value) => 1;
            }
            """);
}
