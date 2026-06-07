// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyOrdering = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.MemberOrderingAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for member ordering inside C# 14 extension blocks (SST1201).</summary>
public class ExtensionBlockOrderingUnitTest
{
    /// <summary>Verifies a method before a property inside an extension block is reported (SST1201).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodBeforePropertyInExtensionReportedAsync()
        => await VerifyOrdering.VerifyAnalyzerAsync(
            """
            public static class Ext
            {
                extension(string text)
                {
                    public void Print()
                    {
                    }

                    public bool {|SST1201:IsEmpty|} => text.Length == 0;
                }
            }
            """);

    /// <summary>Verifies correctly ordered extension members are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrderedExtensionMembersAreCleanAsync()
        => await VerifyOrdering.VerifyAnalyzerAsync(
            """
            public static class Ext
            {
                extension(string text)
                {
                    public bool IsEmpty => text.Length == 0;

                    public void Print()
                    {
                    }
                }
            }
            """);
}
