// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingScopedHashOnlyLocalReportedAsync() =>
        VerifyAnalyzerNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingScopedLocalPassedElsewhereIsCleanAsync() =>
        VerifyAnalyzerNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingScopedLocalWithTwoComputeHashCallsReportedAsync() =>
        VerifyAnalyzerNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThreeArgumentComputeHashIsCleanAsync() =>
        VerifyAnalyzerNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CustomAlgorithmCreateIsCleanAsync() =>
        VerifyAnalyzerNet90Async(
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

        var test = new AnalyzeHashData.Test { ReferenceAssemblies = AnalyzerFrameworks.Net472, TestCode = Source };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the chained syntax gate rejects each near miss and clears its factory result.</summary>
    /// <param name="expression">The candidate invocation.</param>
    /// <param name="matches">Whether the chained shape is eligible.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("SHA256.Create().ComputeHash(bytes)", true)]
    [Arguments("SHA256.Create().ComputeHash()", false)]
    [Arguments("SHA256.Create().ComputeHash(bytes, 0, 1)", false)]
    [Arguments("ComputeHash(bytes)", false)]
    [Arguments("SHA256.Create().Other(bytes)", false)]
    [Arguments("sha.ComputeHash(bytes)", false)]
    [Arguments("SHA256.Create(1).ComputeHash(bytes)", false)]
    [Arguments("Create().ComputeHash(bytes)", false)]
    [Arguments("SHA256.Other().ComputeHash(bytes)", false)]
    public async Task ChainedSyntaxRequiresParameterlessMemberFactoryAsync(string expression, bool matches)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(expression);
        var actual = Psh1400PreferStaticHashDataAnalyzer.IsChainedComputeHashShape(invocation, out var factory);
        await Assert.That(actual).IsEqualTo(matches);
        await Assert.That(factory is not null).IsEqualTo(matches);
    }

    /// <summary>Verifies using statements identify each hash-only declarator independently.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UsingStatementReportsHashOnlyDeclaratorsAsync() =>
        VerifyAnalyzerNet90Async(
            """
            using System.Security.Cryptography;
            class C
            {
                byte[] M(byte[] bytes)
                {
                    using (SHA256 {|PSH1400:first|} = SHA256.Create(), {|PSH1400:second|} = SHA256.Create())
                    {
                        return second.ComputeHash(first.ComputeHash(bytes));
                    }
                }
            }
            """);

    /// <summary>Verifies syntax near misses and non-hash local uses do not suggest replacing the algorithm.</summary>
    /// <param name="statement">The using or hashing statements under analysis.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using (SHA256.Create()) { }")]
    [Arguments("using SHA256 sha = null;")]
    [Arguments("using var sha = Make();")]
    [Arguments("using var sha = SHA256.Create();")]
    [Arguments("using var sha = SHA256.Create(); _ = sha.HashSize;")]
    [Arguments("using var sha = SHA256.Create(); _ = sha.ComputeHash(stream);")]
    [Arguments("using var sha = SHA256.Create(); System.Func<byte[], byte[]> hash = sha.ComputeHash;")]
    [Arguments("using var sha = SHA256.Create(); _ = (sha).ComputeHash(bytes);")]
    [Arguments("using var sha = SHA256.Create(); _ = sha.ComputeHash(bytes); _ = sha.HashSize;")]
    [Arguments("_ = SHA256.Create().ComputeHash(stream);")]
    [Arguments("var sha = SHA256.Create(); _ = sha.ComputeHash(bytes);")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonHashOnlyScopesAreCleanAsync(string statement) =>
        VerifyAnalyzerNet90Async(
            $$"""
            using System.Security.Cryptography;
            class C
            {
                static SHA256 Make() => SHA256.Create();
                void M(byte[] bytes, System.IO.Stream stream)
                {
                    {{statement}}
                }
            }
            """);

    /// <summary>Verifies identical identifier spelling on another symbol does not count as a local use.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SameNamedFieldDoesNotEscapeAlgorithmLocalAsync() =>
        VerifyAnalyzerNet90Async(
            """
            using System.Security.Cryptography;
            class C
            {
                int sha;
                byte[] M(byte[] bytes)
                {
                    using var {|PSH1400:sha|} = SHA256.Create();
                    this.sha = 1;
                    return sha.ComputeHash(bytes);
                }
            }
            """);

    /// <summary>Verifies the runtime gate requires a static method taking a single byte vector.</summary>
    /// <param name="member">The available HashData member.</param>
    /// <param name="reports">Whether the static API is supported.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", false)]
    [Arguments("public byte[] HashData(byte[] bytes) => bytes;", false)]
    [Arguments("public static byte[] HashData(byte[] bytes, int count) => bytes;", false)]
    [Arguments("public static byte[] HashData(int[] bytes) => null;", false)]
    [Arguments("public static byte[] HashData(byte[,] bytes) => null;", false)]
    [Arguments("public static byte[] HashData(byte[][] bytes) => null;", false)]
    [Arguments("public static byte[] HashData(int bytes) => null;", false)]
    [Arguments("public static byte[] HashData;", false)]
    [Arguments("public static byte[] HashData(byte[] bytes) => bytes;", true)]
    [Arguments("public static byte[] HashData(int bytes) => null; public static byte[] HashData(byte[] bytes) => bytes;", true)]
    public Task HashDataSurfaceControlsReportingAsync(string member, bool reports)
    {
        var invocation = reports ? "{|PSH1400:SHA256.Create().ComputeHash(bytes)|}" : "SHA256.Create().ComputeHash(bytes)";
        var local = reports ? "{|PSH1400:sha|}" : "sha";
        var test = new AnalyzeHashData.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = $$"""
                using System.Security.Cryptography;
                namespace System.Security.Cryptography
                {
                    public sealed class SHA256 : System.IDisposable
                    {
                        public static SHA256 Create() => new SHA256();
                        public byte[] ComputeHash(byte[] bytes) => bytes;
                        public void Dispose() { }
                        {{member}}
                    }
                }
                class C
                {
                    byte[] M(byte[] bytes) => {{invocation}};
                    byte[] N(byte[] bytes)
                    {
                        using var {{local}} = SHA256.Create();
                        return sha.ComputeHash(bytes);
                    }
                }
                """,
        };
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectMetadataReferences(projectId, [RuntimeMetadataReferences.CoreLibrary]));
        return test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies bound methods that only resemble a parameterless static factory stay clean.</summary>
    /// <param name="factory">The factory declaration.</param>
    /// <param name="receiver">The factory receiver.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public Factory Create() => this;", "new Factory()")]
    [Arguments("public static Factory Create(int count = 0) => new Factory();", "Factory")]
    [Arguments("public static Factory Create() => new Factory();", "Factory")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnsupportedFactorySymbolsAreCleanAsync(string factory, string receiver) =>
        VerifyAnalyzerNet90Async(
            $$"""
            class Factory : System.IDisposable
            {
                {{factory}}
                public byte[] ComputeHash(byte[] bytes) => bytes;
                public void Dispose() { }
            }
            class C
            {
                byte[] M(byte[] bytes) => {{receiver}}.Create().ComputeHash(bytes);
                byte[] N(byte[] bytes)
                {
                    using var sha = {{receiver}}.Create();
                    return sha.ComputeHash(bytes);
                }
            }
            """);

    /// <summary>Verifies unresolved factory and hashing overloads are ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnresolvedHashingCallsAreCleanAsync() =>
        VerifyAnalyzerNet90Async(
            """
            using System.Security.Cryptography;
            class C
            {
                byte[] M() => SHA256.Create().ComputeHash({|CS1503:42|});
                void N()
                {
                    using var sha = SHA256.Create();
                    _ = sha.ComputeHash({|CS1503:42|});
                }
            }
            """);

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies (where the HashData methods exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyHashData.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAnalyzerNet90Async(string source)
    {
        var test = new AnalyzeHashData.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source };

        await test.RunAsync(CancellationToken.None);
    }
}
