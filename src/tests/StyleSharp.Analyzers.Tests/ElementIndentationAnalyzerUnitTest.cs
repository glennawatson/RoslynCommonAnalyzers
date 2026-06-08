// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyIndentation = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1137ElementIndentationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the element-indentation rule (SST1137).</summary>
public class ElementIndentationAnalyzerUnitTest
{
    /// <summary>Verifies a statement indented differently from its siblings is reported (SST1137).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MisalignedStatementReportedAsync()
        => await VerifyIndentation.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M()
                {
                    var a = 1;
                      {|SST1137:var|} b = 2;
                    var c = 3;
                }
            }
            """);

    /// <summary>Verifies a member indented differently from its siblings is reported (SST1137).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MisalignedMemberReportedAsync()
        => await VerifyIndentation.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int a;
                    {|SST1137:private|} int b;
                private int c;
            }
            """);

    /// <summary>Verifies consistently indented siblings are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConsistentIndentationIsCleanAsync()
        => await VerifyIndentation.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int a;
                private int b;

                private void M()
                {
                    var x = 1;
                    var y = 2;
                }
            }
            """);
}
