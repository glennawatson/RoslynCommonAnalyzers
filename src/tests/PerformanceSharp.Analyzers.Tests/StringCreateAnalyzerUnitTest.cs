// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1209StringCreateAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1209StringCreateAnalyzer"/> (PSH1209 string.Create).</summary>
public class StringCreateAnalyzerUnitTest
{
    /// <summary>Verifies the copy-mutate-rebuild shape is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CopyMutateRebuildIsReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string M(string input)
                {
                    var chars = {|PSH1209:input.ToCharArray()|};
                    for (var i = 0; i < chars.Length; i++)
                    {
                        chars[i] = char.ToUpperInvariant(chars[i]);
                    }

                    return new string(chars);
                }
            }
            """);

    /// <summary>Verifies a copied buffer that is only read stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyBufferIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public char M(string input)
                {
                    var chars = input.ToCharArray();
                    return chars[0];
                }
            }
            """);

    /// <summary>Verifies a mutated buffer that never rebuilds a string stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MutatedWithoutRebuildIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public char[] M(string input)
                {
                    var chars = input.ToCharArray();
                    chars[0] = 'x';
                    return chars;
                }
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        await test.RunAsync(CancellationToken.None);
    }
}
