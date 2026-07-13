// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1225UseEncodingGetStringAnalyzer,
    PerformanceSharp.Analyzers.Psh1225UseEncodingGetStringCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1225UseEncodingGetStringAnalyzer"/> (PSH1225 decode-then-copy).</summary>
public class UseEncodingGetStringAnalyzerUnitTest
{
    /// <summary>Verifies a decode-then-copy is flagged and collapsed to one call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DecodeThenCopyIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Text;

                              public class C
                              {
                                  public string M(byte[] bytes) => {|PSH1225:new string(Encoding.UTF8.GetChars(bytes))|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Text;

                                   public class C
                                   {
                                       public string M(byte[] bytes) => Encoding.UTF8.GetString(bytes);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the index-and-count form carries its arguments across.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexAndCountFormIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Text;

                              public class C
                              {
                                  public string M(byte[] bytes, int count) => {|PSH1225:new string(Encoding.ASCII.GetChars(bytes, 0, count))|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Text;

                                   public class C
                                   {
                                       public string M(byte[] bytes, int count) => Encoding.ASCII.GetString(bytes, 0, count);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an encoding held in a variable is handled just the same.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EncodingVariableIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Text;

                              public class C
                              {
                                  public string M(Encoding encoding, byte[] bytes) => {|PSH1225:new string(encoding.GetChars(bytes))|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Text;

                                   public class C
                                   {
                                       public string M(Encoding encoding, byte[] bytes) => encoding.GetString(bytes);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a char buffer kept as a buffer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CharBufferKeptAsIsIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Text;

            public class C
            {
                public char[] M(byte[] bytes) => Encoding.UTF8.GetChars(bytes);
            }
            """);

    /// <summary>Verifies a string built from a char buffer that came from somewhere else is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringFromUnrelatedBufferIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string M(char[] chars) => new string(chars);
            }
            """);

    /// <summary>Verifies a GetChars on something that is not an encoding is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonEncodingGetCharsIsCleanAsync()
        => await VerifyAsync(
            """
            public class Decoder
            {
                public char[] GetChars(byte[] bytes) => new char[bytes.Length];

                public string GetString(byte[] bytes) => string.Empty;
            }

            public class C
            {
                public string M(Decoder decoder, byte[] bytes) => new string(decoder.GetChars(bytes));
            }
            """);

    /// <summary>
    /// Verifies a decode with no matching <c>GetString</c> sibling is not reported, proving the probe
    /// is load-bearing rather than a hard-coded overload list.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// This encoding adds a <c>GetChars</c> overload of its own that has no <c>GetString</c> twin, so
    /// there is no call to rewrite it into. The rule matches the parameter list of the <c>GetChars</c>
    /// that actually bound against the <c>GetString</c> overloads on the same type, and binds the
    /// rewrite before offering it, so it stays silent here rather than suggesting something that would
    /// not compile.
    /// </remarks>
    [Test]
    public async Task DecodeWithoutMatchingGetStringIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Text;

            public class OddEncoding : UTF8Encoding
            {
                public char[] GetChars(string source) => source.ToCharArray();
            }

            public class C
            {
                public string M(OddEncoding encoding, string source) => new string(encoding.GetChars(source));
            }
            """);

    /// <summary>Verifies a decode inside an expression tree is not rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DecodeInsideExpressionTreeIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Linq.Expressions;
            using System.Text;

            public class C
            {
                public Expression<Func<byte[], string>> M() => bytes => new string(Encoding.UTF8.GetChars(bytes));
            }
            """);

    /// <summary>
    /// Verifies the rule still reports against netstandard2.0, because the overload it suggests exists
    /// there.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// This is the one string rule in the batch whose suggestion is not new: <c>GetString(byte[])</c>
    /// has been on <see cref="System.Text.Encoding"/> since .NET Framework 1.1. So the honest behavior
    /// on netstandard2.0 is to report — the author can act on it and the fix compiles — and staying
    /// silent would be a false negative invented for its own sake. The gate that matters for this rule
    /// is the sibling-overload probe, which <c>DecodeWithoutMatchingGetStringIsCleanAsync</c> pins
    /// down; here the same probe succeeds, on the oldest target the repo supports.
    /// </remarks>
    [Test]
    public async Task NetStandard20StillReportsAsync()
    {
        const string Source = """
                              using System.Text;

                              public class C
                              {
                                  public string M(byte[] bytes) => {|PSH1225:new string(Encoding.UTF8.GetChars(bytes))|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Text;

                                   public class C
                                   {
                                       public string M(byte[] bytes) => Encoding.UTF8.GetString(bytes);
                                   }
                                   """;
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = Source,
            FixedCode = FixedSource,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
