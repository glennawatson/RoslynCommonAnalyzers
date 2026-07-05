// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyUsingDeclaration = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2236UsingDeclarationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2236UsingDeclarationAnalyzer"/>.</summary>
public class UsingDeclarationAnalyzerUnitTest
{
    /// <summary>Verifies a tail-position using block is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TailUsingBlockIsReportedAsync()
        => await RunAsync(
            """
            using System.IO;

            public sealed class C
            {
                public void M()
                {
                    {|SST2236:using|} (var stream = new MemoryStream())
                    {
                        stream.WriteByte(1);
                    }
                }
            }
            """);

    /// <summary>Verifies a using block with later statements is clean because conversion would extend lifetime.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonTailUsingBlockIsCleanAsync()
        => await RunAsync(
            """
            using System.IO;

            public sealed class C
            {
                public void M()
                {
                    using (var stream = new MemoryStream())
                    {
                        stream.WriteByte(1);
                    }

                    System.Console.WriteLine(1);
                }
            }
            """);

    /// <summary>Runs the analyzer verifier with modern reference assemblies.</summary>
    /// <param name="source">The source code to analyze.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RunAsync(string source)
        => await new VerifyUsingDeclaration.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source
        }.RunAsync(CancellationToken.None);
}
