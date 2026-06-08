// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyInheritDoc = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1648InheritDocAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the inheritdoc validity rule (SST1648).</summary>
public class InheritDocAnalyzerUnitTest
{
    /// <summary>Verifies inheritdoc on an element with no base is reported (SST1648).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritDocWithoutBaseReportedAsync()
        => await VerifyInheritDoc.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <inheritdoc/>
                public void {|SST1648:M|}()
                {
                }
            }
            """);

    /// <summary>Verifies inheritdoc on an interface implementation is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritDocOnImplementationIsCleanAsync()
        => await VerifyInheritDoc.VerifyAnalyzerAsync(
            """
            internal interface I
            {
                void M();
            }

            internal class C : I
            {
                /// <inheritdoc/>
                public void M()
                {
                }
            }
            """);
}
