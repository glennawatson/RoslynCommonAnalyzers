// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDocBlankLine = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1644DocumentationHeaderBlankLineAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1644 (documentation headers contain no blank lines).</summary>
public class DocumentationHeaderBlankLineAnalyzerUnitTest
{
    /// <summary>Verifies an interior blank documentation line is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InteriorBlankLineIsReportedAsync()
        => await VerifyDocBlankLine.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>
                /// First line.
            {|SST1644:    ///|}
                /// Second line.
                /// </summary>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies contiguous prose and code elements are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ContiguousAndCodeDocumentationAreCleanAsync()
        => await VerifyDocBlankLine.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>
                /// First line.
                /// Second line.
                /// </summary>
                /// <code>
                ///
                /// </code>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies blank lines inside a nested <c>&lt;code&gt;</c> sample are not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NestedCodeSampleBlankLinesAreCleanAsync()
        => await VerifyDocBlankLine.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Helps manage lifecycle events.</summary>
                /// <remarks>
                /// <para>
                /// Sample usage is shown below.
                /// <code>
                /// <![CDATA[
                /// public App()
                /// {
                ///   Configure();
                ///
                ///   Run();
                /// }
                /// ]]>
                /// </code>
                /// </para>
                /// </remarks>
                public void M()
                {
                }
            }
            """);
}
