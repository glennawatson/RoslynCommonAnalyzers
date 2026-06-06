// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyPropertyValue = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.PropertyValueDocumentationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the property value documentation rules (SST1609/SST1610).</summary>
public class PropertyValueDocumentationAnalyzerUnitTest
{
    /// <summary>Verifies a documented property without a value element is reported (SST1609).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyWithoutValueReportedAsync()
        => await VerifyPropertyValue.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Gets the count.</summary>
                public int {|SST1609:Count|} => 0;
            }
            """);

    /// <summary>Verifies a property whose value element is empty is reported (SST1610).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyWithEmptyValueReportedAsync()
        => await VerifyPropertyValue.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Gets the count.</summary>
                /// <value></value>
                public int {|SST1610:Count|} => 0;
            }
            """);
}
