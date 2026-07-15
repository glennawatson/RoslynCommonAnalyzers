// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCombineFields = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1132DoNotCombineFieldsAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the do-not-combine-fields rule (SST1132).</summary>
public class DoNotCombineFieldsAnalyzerUnitTest
{
    /// <summary>Verifies each field beyond the first in a combined declaration is reported (SST1132).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CombinedFieldsReportedAsync()
        => await VerifyCombineFields.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int a, {|SST1132:b|}, {|SST1132:c|};
            }
            """);

    /// <summary>Verifies single-field declarations are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparateFieldsAreCleanAsync()
        => await VerifyCombineFields.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int a;
                private int b;
            }
            """);

    /// <summary>Verifies each local beyond the first in a combined local declaration is reported (SST1132).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CombinedLocalsReportedAsync()
        => await VerifyCombineFields.VerifyAnalyzerAsync(
            """
            internal class C
            {
                internal void M()
                {
                    int a = 0, {|SST1132:b|} = 1, {|SST1132:c|} = 2;
                }
            }
            """);

    /// <summary>Verifies a single local declaration is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparateLocalsAreCleanAsync()
        => await VerifyCombineFields.VerifyAnalyzerAsync(
            """
            internal class C
            {
                internal void M()
                {
                    int a = 0;
                    int b = 1;
                }
            }
            """);

    /// <summary>Verifies a <c>for</c> initializer that declares several loop variables is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>A single declaration statement is the only way to declare more than one loop variable there.</remarks>
    [Test]
    public async Task ForInitializerWithSeveralVariablesIsCleanAsync()
        => await VerifyCombineFields.VerifyAnalyzerAsync(
            """
            internal class C
            {
                internal void M()
                {
                    for (int i = 0, j = 10; i < j; i++, j--)
                    {
                    }
                }
            }
            """);
}
