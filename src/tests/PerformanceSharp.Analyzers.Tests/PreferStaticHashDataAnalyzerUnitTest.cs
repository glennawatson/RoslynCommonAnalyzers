// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeHashData = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1400PreferStaticHashDataAnalyzer>;
using VerifyHashData = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1400PreferStaticHashDataAnalyzer,
    PerformanceSharp.Analyzers.Psh1400PreferStaticHashDataCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1400 (use the static HashData method for one-shot hashing) and its code fix.</summary>
public class PreferStaticHashDataAnalyzerUnitTest
{
    /// <summary>Verifies a chained create-and-compute call is reported (PSH1400) and rewritten to the static HashData call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ChainedComputeHashReplacedAsync()
    {
        const string Source = """
                              using System.Security.Cryptography;

                              public class C
                              {
                                  public byte[] M(byte[] bytes) => {|PSH1400:SHA256.Create().ComputeHash(bytes)|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Security.Cryptography;

                                   public class C
                                   {
                                       public byte[] M(byte[] bytes) => SHA256.HashData(bytes);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a fully qualified chained call is reported and fixed preserving the qualification.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedChainedComputeHashPreservesQualificationAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public byte[] M(byte[] bytes) => {|PSH1400:System.Security.Cryptography.SHA512.Create().ComputeHash(bytes)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public byte[] M(byte[] bytes) => System.Security.Cryptography.SHA512.HashData(bytes);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the chained MD5 create-and-compute call is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Md5ChainedComputeHashReplacedAsync()
    {
        const string Source = """
                              using System.Security.Cryptography;

                              public class C
                              {
                                  public byte[] M(byte[] bytes) => {|PSH1400:MD5.Create().ComputeHash(bytes)|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Security.Cryptography;

                                   public class C
                                   {
                                       public byte[] M(byte[] bytes) => MD5.HashData(bytes);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a using-scoped algorithm local used only to hash is reported on the declarator (no automated fix).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingScopedHashOnlyLocalReportedAsync()
        => await VerifyAnalyzerNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] bytes)
                {
                    using var {|PSH1400:sha|} = SHA256.Create();
                    return sha.ComputeHash(bytes);
                }
            }
            """);

    /// <summary>Verifies a using-scoped instance also passed to another method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingScopedLocalPassedElsewhereIsCleanAsync()
        => await VerifyAnalyzerNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] bytes)
                {
                    using var sha = SHA256.Create();
                    Log(sha);
                    return sha.ComputeHash(bytes);
                }

                private static void Log(SHA256 sha)
                {
                }
            }
            """);

    /// <summary>Verifies a using-scoped instance with two ComputeHash calls is still reported (hash-only usage).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingScopedLocalWithTwoComputeHashCallsReportedAsync()
        => await VerifyAnalyzerNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] bytes)
                {
                    using var {|PSH1400:sha|} = SHA256.Create();
                    var first = sha.ComputeHash(bytes);
                    return sha.ComputeHash(first);
                }
            }
            """);

    /// <summary>Verifies the three-argument ComputeHash overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreeArgumentComputeHashIsCleanAsync()
        => await VerifyAnalyzerNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] bytes)
                {
                    using var sha = SHA256.Create();
                    return sha.ComputeHash(bytes, 0, bytes.Length);
                }
            }
            """);

    /// <summary>Verifies a custom HashAlgorithm subclass with its own Create factory is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomAlgorithmCreateIsCleanAsync()
        => await VerifyAnalyzerNet90Async(
            """
            using System.Security.Cryptography;

            public sealed class MyHash : SHA256
            {
                public static new MyHash Create() => new();

                public override void Initialize()
                {
                }

                protected override void HashCore(byte[] array, int ibStart, int cbSize)
                {
                }

                protected override byte[] HashFinal() => System.Array.Empty<byte>();
            }

            public class C
            {
                public byte[] M(byte[] bytes) => MyHash.Create().ComputeHash(bytes);
            }
            """);

    /// <summary>Verifies the rule stays silent where the static HashData methods do not exist (net472).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenHashDataUnavailableAsync()
    {
        const string Source = """
                              using System.Security.Cryptography;

                              public class C
                              {
                                  public byte[] M(byte[] bytes) => SHA256.Create().ComputeHash(bytes);
                              }
                              """;

        var test = new AnalyzeHashData.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies (where the HashData methods exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyHashData.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAnalyzerNet90Async(string source)
    {
        var test = new AnalyzeHashData.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
