// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyAttributeLines = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1134AttributesOnSeparateLinesAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the attributes-on-separate-lines rule (SST1134).</summary>
public class AttributesOnSeparateLinesAnalyzerUnitTest
{
    /// <summary>Verifies an attribute sharing a line with its element is reported (SST1134).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AttributeSharingElementLineReportedAsync()
        => await VerifyAttributeLines.VerifyAnalyzerAsync(
            """
            using System;

            {|SST1134:[Serializable]|} internal class C
            {
            }
            """);

    /// <summary>Verifies an attribute on its own line is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AttributeOnOwnLineIsCleanAsync()
        => await VerifyAttributeLines.VerifyAnalyzerAsync(
            """
            using System;

            [Serializable]
            internal class C
            {
            }
            """);

    /// <summary>Verifies inline parameter attributes are not inspected.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterAttributeIsCleanAsync()
        => await VerifyAttributeLines.VerifyAnalyzerAsync(
            """
            using System.Runtime.InteropServices;

            internal class C
            {
                private static void M([In] int x) => _ = x;
            }
            """);
}
