// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2319UnreachableOptionalDefaultAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2319 (an optional parameter a shorter overload always shadows).</summary>
public class Sst2319UnreachableOptionalDefaultAnalyzerUnitTest
{
    /// <summary>Verifies the optional default is reported when a shorter overload takes the required prefix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShadowedOptionalDefaultReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Log(string m)
                {
                }

                public void Log(string m, bool {|SST2319:echo|} = false)
                {
                }
            }
            """);

    /// <summary>Verifies an optional parameter with no matching shorter overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoneOptionalParameterIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Log(string m, bool echo = false)
                {
                }
            }
            """);

    /// <summary>Verifies overloads that differ in the required prefix type are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentPrefixTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Log(int m)
                {
                }

                public void Log(string m, bool echo = false)
                {
                }
            }
            """);
}
