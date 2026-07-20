// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzePbkdf2 = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1003Pbkdf2IterationCountAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1003 (a PBKDF2 one-shot must use a sufficient iteration count).</summary>
public class Pbkdf2IterationCountAnalyzerUnitTest
{
    /// <summary>Verifies a low literal iteration count on a positional Pbkdf2 call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LowLiteralIterationsReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] password, byte[] salt)
                    => Rfc2898DeriveBytes.Pbkdf2(password, salt, {|SES1003:1000|}, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies a low iteration count passed by name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedLowIterationsReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] password, byte[] salt)
                    => Rfc2898DeriveBytes.Pbkdf2(password, salt, hashAlgorithm: HashAlgorithmName.SHA256, iterations: {|SES1003:1000|}, outputLength: 32);
            }
            """);

    /// <summary>Verifies a low count from a <c>const</c> field (a compile-time constant) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstFieldLowIterationsReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                private const int Iterations = 5000;

                public byte[] M(byte[] password, byte[] salt)
                    => Rfc2898DeriveBytes.Pbkdf2(password, salt, {|SES1003:Iterations|}, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies the span-based Pbkdf2 overload is also reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpanOverloadLowIterationsReportedAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt)
                    => Rfc2898DeriveBytes.Pbkdf2(password, salt, {|SES1003:1|}, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies a count at the default floor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AtFloorIterationsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] password, byte[] salt)
                    => Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies a count above the default floor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AboveFloorIterationsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] password, byte[] salt)
                    => Rfc2898DeriveBytes.Pbkdf2(password, salt, 600000, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies a non-constant iteration count (a method parameter) stays silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantIterationsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] password, byte[] salt, int iterations)
                    => Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies a non-constant count read from configuration stays silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfigDrivenIterationsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                private int _iterations;

                public byte[] M(byte[] password, byte[] salt)
                    => Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, 32);
            }
            """);

    /// <summary>Verifies a Pbkdf2 method on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedPbkdf2MethodCleanAsync()
        => await VerifyNet90Async(
            """
            public static class Kdf
            {
                public static byte[] Pbkdf2(byte[] password, byte[] salt, int iterations)
                    => password;
            }

            public class C
            {
                public byte[] M(byte[] password, byte[] salt)
                    => Kdf.Pbkdf2(password, salt, 10);
            }
            """);

    /// <summary>Verifies a raised floor reports a count that the default would have allowed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RaisedFloorReportsMidCountAsync()
    {
        var test = new AnalyzePbkdf2.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using System.Security.Cryptography;

                       public class C
                       {
                           public byte[] M(byte[] password, byte[] salt)
                               => Rfc2898DeriveBytes.Pbkdf2(password, salt, {|SES1003:200000|}, HashAlgorithmName.SHA256, 32);
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.SES1003.iterations = 600000

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a lowered floor via the project-wide key allows a count the default would flag.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoweredProjectWideFloorAllowsSmallCountAsync()
    {
        var test = new AnalyzePbkdf2.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using System.Security.Cryptography;

                       public class C
                       {
                           public byte[] M(byte[] password, byte[] salt)
                               => Rfc2898DeriveBytes.Pbkdf2(password, salt, 20000, HashAlgorithmName.SHA256, 32);
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.iterations = 10000

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a nonsensical floor value falls back to the default and still reports a low count.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonsensicalFloorFallsBackToDefaultAsync()
    {
        var test = new AnalyzePbkdf2.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using System.Security.Cryptography;

                       public class C
                       {
                           public byte[] M(byte[] password, byte[] salt)
                               => Rfc2898DeriveBytes.Pbkdf2(password, salt, {|SES1003:1000|}, HashAlgorithmName.SHA256, 32);
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.SES1003.iterations = not-a-number

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the obsolete constructor surface is not reported (that shape is covered elsewhere).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorLowIterationsNotReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public byte[] M(byte[] password, byte[] salt, int cb)
                {
                    using var derive = new Rfc2898DeriveBytes(password, salt, 1000, HashAlgorithmName.SHA256);
                    return derive.GetBytes(cb);
                }
            }
            """);

    /// <summary>Verifies the rule stays silent on a framework whose <c>Rfc2898DeriveBytes</c> has no <c>Pbkdf2</c> one-shot.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenPbkdf2UnavailableAsync()
    {
        const string Source = """
                              using System.Security.Cryptography;

                              public class C
                              {
                                  public byte[] M(byte[] password, byte[] salt, int cb)
                                  {
                                      using var derive = new Rfc2898DeriveBytes(password, salt, 1000);
                                      return derive.GetBytes(cb);
                                  }
                              }
                              """;

        var test = new AnalyzePbkdf2.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where <c>Pbkdf2</c> exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzePbkdf2.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
