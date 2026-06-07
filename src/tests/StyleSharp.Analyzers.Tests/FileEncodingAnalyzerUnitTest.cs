// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

using VerifyEncoding = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.FileEncodingAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the file-encoding rules (SST1412/SST1450).</summary>
public class FileEncodingAnalyzerUnitTest
{
    /// <summary>Verifies a file without a byte order mark is reported when a BOM is required (SST1412).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileWithoutBomReportedAsync()
    {
        var test = new VerifyEncoding.Test
        {
            TestCode = """
                internal class C
                {
                }

                """,

            // A file-start (position 0) diagnostic cannot be suppressed by a #pragma, so skip the suppression check.
            TestBehaviors = TestBehaviors.SkipSuppressionCheck
        };
        test.ExpectedDiagnostics.Add(VerifyEncoding.Diagnostic("SST1412").WithSpan(1, 1, 1, 1));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a file with a byte order mark is reported when no BOM is required (SST1450).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileWithBomReportedAsync()
    {
        var content = SourceText.From(
            """
            internal class C
            {
            }

            """,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var test = new VerifyEncoding.Test
        {
            TestState = { Sources = { ("/0/Test0.cs", content) } },
            TestBehaviors = TestBehaviors.SkipSuppressionCheck
        };
        test.ExpectedDiagnostics.Add(VerifyEncoding.Diagnostic("SST1450").WithSpan(1, 1, 1, 1));
        await test.RunAsync(CancellationToken.None);
    }
}
